using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Exceptions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Revert;
using optimizerDuck.Domain.UI;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Services.Revert;

public class RevertManager(
    ILogger<RevertManager> _logger,
    ShellService _shell,
    PowerPlanService _powerPlans,
    TimeProvider _time
)
{
    private const int SchemaVersion = 1;
    private const int FileLockTimeoutSeconds = 30;

    private static readonly Lazy<Dictionary<string, Func<JObject, IRevertStep>>> _stepRegistry =
        new(BuildStepRegistry);

    /// <summary>Per item file locks. Visible to the test assembly so a stuck lock can be simulated.</summary>
    internal static readonly ConcurrentDictionary<Guid, SemaphoreSlim> FileLocks = new();

    private readonly TimeSpan _fileLockTimeout = TimeSpan.FromSeconds(FileLockTimeoutSeconds);

    /// <summary>Test seam: the same manager with a shorter file lock timeout.</summary>
    internal RevertManager(
        ILogger<RevertManager> logger,
        ShellService shell,
        PowerPlanService powerPlans,
        TimeProvider time,
        TimeSpan fileLockTimeout
    )
        : this(logger, shell, powerPlans, time)
    {
        _fileLockTimeout = fileLockTimeout;
    }

    /// <summary>
    ///     Persists revert steps from a <see cref="ChangeSet"/>. Appends every successful change as a new entry with a fresh index. No payload-based dedupe: two executions of the same command are two real executions; dropping either loses revert coverage.
    ///     Reverting extra entries is harmless (LIFO ends at the original backup).
    /// </summary>
    public async Task SaveRevertDataAsync(
        ChangeSet changes,
        Guid id,
        string name,
        CancellationToken cancellationToken = default
    )
    {
        // Compensation is persisted whenever it exists, whether the step reported success or a
        // partial failure: a step that was refused after it had already changed something still
        // needs its previous state recorded.
        var incoming = changes
            .Changes.Where(c => c.Revert != null)
            .OrderBy(c => c.Index)
            .ToList();

        // A step that modified the system must carry the data needed to undo it. The filter
        // above drops entries without compensation, so a Change arriving here without one is a
        // provider defect rather than a legitimate skip.
        foreach (
            var defect in changes.Changes.Where(c =>
                c.Ok && c.Kind == ChangeKind.Change && c.Revert == null
            )
        )
            _logger.LogWarning(
                "Recorded change {Step} for {Name} carries no revert data, so undo does not cover it",
                defect.Name,
                name
            );

        if (incoming.Count == 0)
            return;

        var filePath = GetFilePath(id);
        var lockObj = await AcquireFileLockAsync(id).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var data =
                await LoadForWriteAsync(filePath).ConfigureAwait(false)
                ?? new RevertData
                {
                    SchemaVersion = SchemaVersion,
                    OptimizationId = id,
                    OptimizationName = name,
                    AppliedAt = _time.GetLocalNow().DateTime,
                    Steps = Array.Empty<RevertStepData?>(),
                };

            data.OptimizationName = name;

            // Every incoming change appends its own entry with a fresh index.
            // No payload-based dedupe: two executions of the same command are
            // two real executions, and dropping either loses revert coverage.
            // Reverting extra entries is harmless (LIFO ends at the original
            // backup); dropping one silently is not. Indexes are never reused
            // so on-disk entries stay stable across saves.
            var nextIndex =
                data.Steps.Where(s => s != null).Select(s => s!.Index).DefaultIfEmpty(0).Max() + 1;
            var merged = data.Steps.Where(s => s != null).Cast<RevertStepData>().ToList();

            foreach (var change in incoming)
            {
                var step = new RevertStepData
                {
                    Index = nextIndex++,
                    Type = change.Revert!.Type,
                    Data = change.Revert.ToData(),
                };
                merged.Add(step);
            }

            var maxIndex = merged.Count > 0 ? merged.Max(s => s.Index) : 0;
            var steps = new RevertStepData?[maxIndex];
            foreach (var step in merged)
                steps[step.Index - 1] = step;
            data.Steps = steps;

            data.SchemaVersion = SchemaVersion;
            // CancellationToken.None: a cancelled write would leave a half-written file;
            // cancellation is honoured by the guard above instead.
            await WriteJsonAtomicAsync(filePath, data, CancellationToken.None)
                .ConfigureAwait(false);

            _logger.LogInformation("Saved {Total} revert steps to {Path}", merged.Count, filePath);
        }
        finally
        {
            lockObj.Release();
        }
    }

    /// <summary>Reverts the optimization using its persisted revert steps.</summary>
    public async Task<RevertResult> RevertAsync(
        IOptimization optimization,
        IProgress<ProcessingProgress>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var steps = await LoadStepsAsync(optimization.Id).ConfigureAwait(false);
        if (steps.Count == 0)
        {
            var path = GetFilePath(optimization.Id);
            var unreadable = File.Exists(path) ? path : FindParkedFile(path);
            if (unreadable is not null)
                return new RevertResult
                {
                    Success = false,
                    Message = Loc.Instance[
                        "Revert.Error.UnreadableData",
                        optimization.Name,
                        unreadable
                    ],
                };

            return new RevertResult
            {
                Success = false,
                Message = Loc.Instance["Revert.Error.NoDataFound", optimization.Name],
            };
        }

        var failedSteps = new List<Change>();
        var cleanupFailed = false;
        var sortedSteps = steps.OrderByDescending(s => s.Index).ToList();
        var total = sortedSteps.Count;

        for (var i = 0; i < total; i++)
        {
            var (idx, step) = sortedSteps[i];
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = total - i;
            progress?.Report(
                new ProcessingProgress
                {
                    Message = Loc.Instance[
                        "Optimization.Revert.ExecutingStep",
                        remaining,
                        total,
                        step.Type
                    ],
                    Value = remaining,
                    Total = total,
                }
            );

            try
            {
                var context = new RevertContext
                {
                    Shell = _shell,
                    PowerPlans = _powerPlans,
                    Logger = _logger,
                };
                if (!await step.ExecuteAsync(context, _logger).ConfigureAwait(false))
                    throw new Exception(Loc.Instance["Revert.Error.StepFailed"]);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Revert step {StepType} failed for {Optimization}",
                    step.Type,
                    optimization.OptimizationKey
                );

                var stepEx = ex as StepExecutionException;
                failedSteps.Add(
                    new Change
                    {
                        Index = idx,
                        Name = step.Type,
                        Description = step.Description,
                        Ok = false,
                        Error = stepEx?.Message ?? ex.Message,
                        ErrorDetail = stepEx?.ErrorDetail,
                        Retry = async _ => new OpResult(
                            await step.ExecuteAsync(
                                    new RevertContext
                                    {
                                        Shell = _shell,
                                        PowerPlans = _powerPlans,
                                        Logger = _logger,
                                    },
                                    _logger
                                )
                                .ConfigureAwait(false)
                        ),
                    }
                );
            }
        }

        if (failedSteps.Count == 0)
        {
            RemoveRevertData(optimization.Id, optimization.OptimizationKey);
        }
        else if (failedSteps.Count < total)
        {
            // Partial revert: prune every succeeded step with a single
            // load-modify-write so concurrent readers never observe a
            // half-pruned file.
            var failedIndexes = failedSteps.Select(s => s.Index).ToHashSet();
            var succeededIndexes = sortedSteps
                .Select(s => s.Index)
                .Where(idx => !failedIndexes.Contains(idx))
                .ToList();
            // CancellationToken.None: the prune must run to completion, otherwise the
            // file would list steps that already reverted successfully.
            try
            {
                await RemoveRevertStepsAtIndexesAsync(
                        optimization.Id,
                        optimization.OptimizationKey,
                        succeededIndexes,
                        CancellationToken.None
                    )
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // The steps themselves reverted, so the caller still gets a result. Only the
                // cleanup of the data they came from failed, and a leftover file is retried.
                _logger.LogError(
                    ex,
                    "Revert data for {Name} could not be cleaned up",
                    optimization.OptimizationKey
                );
                cleanupFailed = true;
            }
        }

        return new RevertResult
        {
            Success = failedSteps.Count == 0,
            AllStepsFailed = failedSteps.Count == total,
            Message =
                failedSteps.Count == total
                    ? Loc.Instance["Optimization.Revert.Error.Failed", optimization.Name]
                : failedSteps.Count > 0
                    ? Loc.Instance[
                        "Optimization.Revert.Error.FailedWithSteps",
                        optimization.Name,
                        failedSteps.Count
                    ]
                : Loc.Instance["Optimization.Revert.Success", optimization.Name],
            FailedSteps = failedSteps,
            CleanupFailed = cleanupFailed,
        };
    }

    /// <summary>
    ///     Appends a recovered revert step (e.g. after a successful retry) as
    ///     a new entry. Never overwrites: with compact indexes the original
    ///     sequence number no longer addresses an empty slot, so writing into
    ///     it would destroy an unrelated entry. Revert runs LIFO, so the
    ///     newest backup is undone first and the original backup still
    ///     restores the true original state.
    /// </summary>
    public async Task AppendRevertStepAsync(
        Guid id,
        string name,
        IRevertStep step,
        CancellationToken cancellationToken = default
    )
    {
        var filePath = GetFilePath(id);
        var lockObj = await AcquireFileLockAsync(id).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var data =
                await LoadForWriteAsync(filePath).ConfigureAwait(false)
                ?? new RevertData
                {
                    SchemaVersion = SchemaVersion,
                    OptimizationId = id,
                    OptimizationName = name,
                    AppliedAt = _time.GetLocalNow().DateTime,
                    Steps = Array.Empty<RevertStepData?>(),
                };

            data.OptimizationName = name;
            var nextIndex =
                data.Steps.Where(s => s != null).Select(s => s!.Index).DefaultIfEmpty(0).Max() + 1;
            var merged = data.Steps.Where(s => s != null).Cast<RevertStepData>().ToList();
            merged.Add(
                new RevertStepData
                {
                    Index = nextIndex,
                    Type = step.Type,
                    Data = step.ToData(),
                }
            );

            var maxIndex = merged.Max(s => s.Index);
            var steps = new RevertStepData?[maxIndex];
            foreach (var entry in merged)
                steps[entry.Index - 1] = entry;
            data.Steps = steps;

            data.SchemaVersion = SchemaVersion;
            await WriteJsonAtomicAsync(filePath, data, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Appended revert step at index {Index} for {Id} (total: {Total} steps)",
                nextIndex,
                id,
                merged.Count
            );
        }
        finally
        {
            lockObj.Release();
        }
    }

    public async Task RemoveRevertStepAtIndexAsync(Guid id, string? name, int stepIndex)
    {
        if (stepIndex <= 0)
            return;
        await RemoveRevertStepsAtIndexesAsync(id, name, [stepIndex]).ConfigureAwait(false);
    }

    /// <summary>
    ///     Prunes several steps with a single load-modify-write so a crash
    ///     can never leave a half-pruned file behind.
    /// </summary>
    public async Task RemoveRevertStepsAtIndexesAsync(
        Guid id,
        string? name,
        IReadOnlyCollection<int> stepIndexes,
        CancellationToken cancellationToken = default
    )
    {
        var targets = stepIndexes.Where(i => i > 0).ToHashSet();
        if (targets.Count == 0)
            return;

        var filePath = GetFilePath(id);
        var lockObj = await AcquireFileLockAsync(id).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var data = await LoadAsync(filePath, _logger).ConfigureAwait(false);
            if (data == null || data.Steps.Length == 0)
                return;

            foreach (var index in targets)
            {
                if (index <= data.Steps.Length)
                    data.Steps[index - 1] = null;
            }

            if (data.Steps.All(s => s == null))
            {
                Remove(id, name);
                return;
            }

            data.SchemaVersion = SchemaVersion;
            await WriteJsonAtomicAsync(filePath, data, cancellationToken).ConfigureAwait(false);

            var remainingSteps = data.Steps.Count(s => s != null);
            _logger.LogInformation(
                "Removed {Count} revert steps for {Id} (remaining: {Remaining} steps)",
                targets.Count,
                id,
                remainingSteps
            );
        }
        finally
        {
            lockObj.Release();
        }
    }

    /// <summary>
    ///     Whether the item has revert data at all, readable or not. A file that exists but
    ///     cannot be parsed still means the item was applied once, so it is never presented as
    ///     untouched, and neither is a file that was parked after a failed read.
    /// </summary>
    public static Task<bool> IsAppliedAsync(Guid id)
    {
        var path = GetFilePath(id);
        return Task.FromResult(File.Exists(path) || FindParkedFile(path) is not null);
    }

    public static async Task<RevertData?> GetRevertDataAsync(Guid id)
    {
        return await LoadAsync(GetFilePath(id)).ConfigureAwait(false);
    }

    public static void ClearAllRevertData(ILogger logger)
    {
        if (Directory.Exists(Shared.RevertDirectory))
        {
            foreach (var f in Directory.GetFiles(Shared.RevertDirectory))
            {
                try
                {
                    File.Delete(f);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to delete {File}", f);
                }
            }
        }

        // NOTE: file locks are intentionally left alone. Disposing a
        // SemaphoreSlim while another thread holds or waits on it throws
        // ObjectDisposedException; the entries are cheap and safely reused.
    }

    /// <summary>
    ///     Loads revert data for a write. The loader returns null both for "no file" and for
    ///     "cannot be read"; only the second case has something to preserve, and the write that
    ///     follows would replace the last copy of a backup, so it is parked first.
    /// </summary>
    private async Task<RevertData?> LoadForWriteAsync(string filePath)
    {
        var loaded = await LoadAsync(filePath, _logger).ConfigureAwait(false);
        if (loaded is not null || !File.Exists(filePath))
            return loaded;

        var parkedPath = ParkUnreadable(filePath);
        _logger.LogWarning(
            "Revert data at {Path} could not be read; kept as {Parked}",
            filePath,
            parkedPath
        );
        return null;
    }

    /// <summary>The newest parked copy of an unreadable revert file, or null when there is none.</summary>
    private static string? FindParkedFile(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return null;

        var matches = Directory.GetFiles(directory, Path.GetFileName(path) + ".unreadable-*");
        if (matches.Length == 0)
            return null;

        return matches.OrderBy(static m => m, StringComparer.Ordinal).Last();
    }

    /// <summary>Moves an unreadable revert file aside, keeping its bytes, and returns the new path.</summary>
    private string ParkUnreadable(string path)
    {
        var stamp = _time.GetUtcNow().ToString("yyyyMMddHHmmss");
        var parked = $"{path}.unreadable-{stamp}";

        var attempt = 1;
        while (File.Exists(parked))
            parked = $"{path}.unreadable-{stamp}-{attempt++}";

        File.Move(path, parked);
        return parked;
    }

    private static string GetFilePath(Guid id)
    {
        return Path.Combine(Shared.RevertDirectory, $"{id}.json");
    }

    private static async Task<RevertData?> LoadAsync(string path, ILogger? logger = null)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            for (var i = 0; i < 3; i++)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(json))
                        return null;

                    var data = JsonConvert.DeserializeObject<RevertData>(json);
                    if (data == null)
                        return null;

                    // Validate the file is within the expected revert directory (path traversal guard)
                    var resolvedPath = Path.GetFullPath(path);
                    var revertDir = Path.GetFullPath(Shared.RevertDirectory);
                    if (!revertDir.EndsWith(Path.DirectorySeparatorChar))
                        revertDir += Path.DirectorySeparatorChar;

                    if (!resolvedPath.StartsWith(revertDir, StringComparison.OrdinalIgnoreCase))
                    {
                        LogCorruptRevertFile(
                            logger,
                            path,
                            new InvalidOperationException(
                                "Revert file path outside expected directory"
                            )
                        );
                        return null;
                    }

                    var canonicalRevertDir = Path.GetFullPath(revertDir)
                        .TrimEnd(Path.DirectorySeparatorChar);
                    var canonicalPath = Path.GetFullPath(resolvedPath);
                    if (
                        !canonicalPath.StartsWith(
                            canonicalRevertDir + Path.DirectorySeparatorChar,
                            StringComparison.OrdinalIgnoreCase
                        )
                        && !string.Equals(
                            canonicalPath,
                            canonicalRevertDir,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        LogCorruptRevertFile(
                            logger,
                            path,
                            new InvalidOperationException(
                                "Revert file path escapes canonical revert directory"
                            )
                        );
                        return null;
                    }
                    // Validate schema version
                    if (data.SchemaVersion != SchemaVersion)
                    {
                        LogCorruptRevertFile(
                            logger,
                            path,
                            new InvalidOperationException(
                                $"Unsupported schema version {data.SchemaVersion} (expected {SchemaVersion})"
                            )
                        );
                        return null;
                    }

                    return data;
                }
                catch (IOException) when (i < 2)
                {
                    await Task.Delay(50).ConfigureAwait(false);
                }
            }
            return null;
        }
        catch (JsonException ex)
        {
            LogCorruptRevertFile(logger, path, ex);
            return null;
        }
        catch (Exception ex)
        {
            LogCorruptRevertFile(logger, path, ex);
            return null;
        }
    }

    private static void LogCorruptRevertFile(ILogger? logger, string path, Exception ex)
    {
        if (logger != null)
        {
            logger.LogWarning(
                ex,
                "Corrupt or invalid revert file {Path}: {Message}",
                path,
                ex.Message
            );
        }
        else
        {
            Serilog
                .Log.ForContext<RevertManager>()
                .Warning(ex, "Corrupt or invalid revert file {Path}: {Message}", path, ex.Message);
        }
    }

    public void RemoveRevertData(Guid id, string? name = null)
    {
        Remove(id, name);
        // Lock entry intentionally kept: disposing a SemaphoreSlim that a
        // concurrent operation holds or waits on throws. Entries are reused.
    }

    private static async Task WriteJsonAtomicAsync(
        string path,
        RevertData data,
        CancellationToken cancellationToken = default
    )
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        var tempPath = path + ".tmp";
        await using (
            var stream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough | FileOptions.Asynchronous
            )
        )
        await using (var writer = new StreamWriter(stream, Encoding.UTF8))
        {
            await writer.WriteAsync(json).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path))
                File.Replace(tempPath, path, destinationBackupFileName: null);
            else
                File.Move(tempPath, path);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Best-effort cleanup
                }
            }
            throw;
        }
    }

    /// <summary>
    ///     Deletes stale <c>.tmp</c> files left by crashes between temp-write
    ///     and replace. Called once at startup; never touches live <c>.json</c> files.
    /// </summary>
    public static void RemoveOrphanedTempFiles(ILogger? logger = null)
    {
        if (!Directory.Exists(Shared.RevertDirectory))
            return;
        foreach (var tmp in Directory.GetFiles(Shared.RevertDirectory, "*.tmp"))
        {
            try
            {
                File.Delete(tmp);
                logger?.LogInformation("Removed orphaned revert temp file {Path}", tmp);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to remove orphaned temp file {Path}", tmp);
            }
        }
    }

    private async Task<SemaphoreSlim> AcquireFileLockAsync(Guid id)
    {
        var lockObj = FileLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        if (!await lockObj.WaitAsync(_fileLockTimeout).ConfigureAwait(false))
            throw new TimeoutException(
                string.Format("Timed out waiting for revert file lock ({0}).", id)
            );

        return lockObj;
    }

    private async Task<List<(int Index, IRevertStep Step)>> LoadStepsAsync(Guid id)
    {
        var data = await LoadAsync(GetFilePath(id), _logger).ConfigureAwait(false);
        if (data == null || data.Steps.Length == 0)
            return [];

        var seenIndexes = new HashSet<int>();
        var result = new List<(int, IRevertStep)>();

        foreach (var stepData in data.Steps.Where(s => s != null))
        {
            if (stepData!.Index <= 0)
            {
                _logger.LogWarning(
                    "Skipping step with invalid index {Index} in revert data for {Id}",
                    stepData.Index,
                    id
                );
                continue;
            }

            if (!seenIndexes.Add(stepData.Index))
            {
                _logger.LogWarning(
                    "Skipping step with duplicate index {Index} in revert data for {Id}",
                    stepData.Index,
                    id
                );
                continue;
            }

            var step = DeserializeStep(stepData.Type, stepData.Data);
            if (step != null)
            {
                result.Add((stepData.Index, step));
            }
            else
            {
                // Never silently drop: an unknown type becomes a step that
                // fails loudly at revert time, preserving the index layout
                // and telling the user exactly which data is unloadable.
                _logger.LogWarning(
                    "Unknown revert step type '{Type}' for optimization {Id}",
                    stepData.Type,
                    id
                );
                result.Add((stepData.Index, new CorruptedStep(stepData.Type, stepData.Data)));
            }
        }

        return result.OrderBy(x => x.Item1).ToList();
    }

    private void Remove(Guid id, string? name = null)
    {
        try
        {
            var path = GetFilePath(id);
            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.LogInformation(
                    "Removed revert data for {Name}: {Path}",
                    name ?? id.ToString(),
                    path
                );
            }

            var tempPath = path + ".tmp";
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove revert data for {Id}", id);
        }
    }

    private static Dictionary<string, Func<JObject, IRevertStep>> BuildStepRegistry()
    {
        var dict = new Dictionary<string, Func<JObject, IRevertStep>>();
        foreach (var type in ReflectionHelper.FindImplementationsInLoadedAssemblies<IRevertStep>())
        {
            if (type.IsAbstract || type.IsInterface || type == typeof(CorruptedStep))
                continue;

            if (type.GetConstructor(Type.EmptyTypes) == null)
                continue;

            var method = type.GetMethod("FromData", BindingFlags.Static | BindingFlags.Public);
            if (method == null)
                continue;

            try
            {
                var instance = (IRevertStep)Activator.CreateInstance(type)!;
                dict[instance.Type] = data => (IRevertStep)method.Invoke(null, [data])!;
            }
            catch (Exception ex)
            {
                Serilog
                    .Log.ForContext<RevertManager>()
                    .Warning(
                        ex,
                        "Failed to register revert step {Type}: {Message}",
                        type.FullName,
                        ex.Message
                    );
            }
        }

        return dict;
    }

    private IRevertStep? DeserializeStep(string type, JToken data)
    {
        if (data is not JObject obj)
            return null;
        if (_stepRegistry.Value.TryGetValue(type, out var factory))
            return factory(obj);

        _logger.LogWarning("Unknown revert step type: {Type}", type);
        return null;
    }

    /// <summary>
    ///     Placeholder for persisted data whose step type is not registered
    ///     (downgrade, removed plugin). Fails loudly at revert time instead
    ///     of vanishing silently, and round-trips the raw payload so a
    ///     future version can still read it.
    /// </summary>
    private sealed class CorruptedStep(string rawType, JObject rawData) : IRevertStep
    {
        public string Type => rawType;

        public string Description =>
            ServiceStrings.Format(ServiceStrings.RevertDataUnloadableDescription, rawType);

        public Task<bool> ExecuteAsync(RevertContext _, ILogger logger)
        {
            throw new StepExecutionException(
                ServiceStrings.Format(ServiceStrings.RevertDataUnknownType, rawType),
                null
            );
        }

        public JObject ToData()
        {
            return rawData;
        }
    }
}
