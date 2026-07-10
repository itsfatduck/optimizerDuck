using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
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

namespace optimizerDuck.Services.Revert;

public class RevertManager(ILogger<RevertManager> _logger, ILoggerFactory _loggerFactory)
{
    private const int SchemaVersion = 1;
    private const int FileLockTimeoutSeconds = 30;

    private static readonly Lazy<Dictionary<string, Func<JObject, IRevertStep>>> _stepRegistry =
        new(BuildStepRegistry);

    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _fileLocks = new();

    public async Task SaveRevertDataAsync(ExecutionScope scope)
    {
        var successfulSteps = scope
            .ExecutedSteps.Where(s => s.Success && s.RevertStep != null)
            .ToList();

        if (successfulSteps.Count == 0)
            return;

        var maxIndex = successfulSteps.Max(s => s.Index);
        var steps = new RevertStepData?[maxIndex];

        foreach (var executedStep in successfulSteps)
        {
            var arrayIndex = executedStep.Index - 1;
            steps[arrayIndex] = new RevertStepData
            {
                Index = executedStep.Index,
                Type = executedStep.RevertStep!.Type,
                Data = executedStep.RevertStep.ToData(),
            };
        }

        var optimizationId = scope.OptimizationId!.Value;
        await WriteJsonAsync(
            optimizationId,
            GetFilePath(optimizationId),
            new RevertData
            {
                SchemaVersion = SchemaVersion,
                OptimizationId = optimizationId,
                OptimizationName = scope.OptimizationName ?? scope.OptimizationKey!,
                AppliedAt = DateTime.Now,
                Steps = steps,
            }
        );
    }

    public async Task<RevertResult> RevertAsync(
        IOptimization optimization,
        IProgress<ProcessingProgress>? progress = null
    )
    {
        var operationLogger = _loggerFactory.CreateLogger<RevertManager>();
        using var scope = ExecutionScope.BeginForLogging(
            optimization.Id,
            optimization.OptimizationKey,
            operationLogger
        );

        var steps = await LoadStepsAsync(optimization.Id);
        if (steps.Count == 0)
            return new RevertResult
            {
                Success = false,
                Message = string.Format(Translations.Revert_Error_NoDataFound, optimization.Name),
            };

        var failedSteps = new List<OperationStepResult>();
        var sortedSteps = steps.OrderByDescending(s => s.Index).ToList();
        var total = sortedSteps.Count;

        for (var i = 0; i < total; i++)
        {
            var (idx, step) = sortedSteps[i];
            var remaining = total - i;
            progress?.Report(
                new ProcessingProgress
                {
                    Message = string.Format(
                        Translations.Optimization_Revert_ExecutingStep,
                        remaining,
                        total,
                        step.Type
                    ),
                    Value = remaining,
                    Total = total,
                }
            );

            try
            {
                if (!await step.ExecuteAsync())
                    throw new Exception(Translations.Revert_Error_StepFailed);
            }
            catch (Exception ex)
            {
                operationLogger.LogError(
                    ex,
                    "Revert step {StepType} failed for {Optimization}",
                    step.Type,
                    optimization.OptimizationKey
                );

                var stepEx = ex as StepExecutionException;
                failedSteps.Add(
                    new OperationStepResult
                    {
                        Index = idx,
                        Name = step.Type,
                        Description = step.Description,
                        Success = false,
                        Error = stepEx?.Message ?? ex.Message,
                        ErrorDetail = stepEx?.ErrorDetail,
                        RetryAction = () => step.ExecuteAsync(),
                    }
                );
            }
        }

        if (failedSteps.Count < total)
            RemoveRevertData(optimization.Id, optimization.OptimizationKey);

        if (failedSteps.Count == 0)
        {
            if (_fileLocks.TryRemove(optimization.Id, out var sem))
            {
                sem.Dispose();
            }
        }

        return new RevertResult
        {
            Success = failedSteps.Count == 0,
            AllStepsFailed = failedSteps.Count == total,
            Message =
                failedSteps.Count == total
                    ? string.Format(
                        Translations.Optimization_Revert_Error_Failed,
                        optimization.Name
                    )
                : failedSteps.Count > 0
                    ? string.Format(
                        Translations.Optimization_Revert_Error_FailedWithSteps,
                        optimization.Name,
                        failedSteps.Count
                    )
                : string.Format(Translations.Optimization_Revert_Success, optimization.Name),
            FailedSteps = failedSteps,
        };
    }

    public async Task UpsertRevertStepAtIndexAsync(
        Guid id,
        string name,
        int stepIndex,
        IRevertStep step
    )
    {
        if (stepIndex <= 0)
            throw new ArgumentException("Step index must be greater than 0", nameof(stepIndex));

        var filePath = GetFilePath(id);
        var lockObj = await AcquireFileLockAsync(id);
        try
        {
            var data =
                await LoadAsyncUnlocked(filePath)
                ?? new RevertData
                {
                    SchemaVersion = SchemaVersion,
                    OptimizationId = id,
                    OptimizationName = name,
                    Steps = Array.Empty<RevertStepData?>(),
                };

            if (data.Steps.Length < stepIndex)
            {
                var newSteps = new RevertStepData?[stepIndex];
                Array.Copy(data.Steps, newSteps, data.Steps.Length);
                data.Steps = newSteps;
            }

            data.Steps[stepIndex - 1] = new RevertStepData
            {
                Index = stepIndex,
                Type = step.Type,
                Data = step.ToData(),
            };

            data.SchemaVersion = SchemaVersion;
            await WriteJsonAtomicAsync(filePath, data);
        }
        finally
        {
            lockObj.Release();
        }
    }

    public static async Task<bool> IsAppliedAsync(Guid id)
    {
        var data = await LoadAsync(GetFilePath(id));
        return data is { Steps.Length: > 0 };
    }

    public static async Task<RevertData?> GetRevertDataAsync(Guid id)
    {
        return await LoadAsync(GetFilePath(id));
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

        var semaphores = _fileLocks.Values.ToList();
        _fileLocks.Clear();
        foreach (var sem in semaphores)
        {
            sem.Dispose();
        }
    }

    private static string GetFilePath(Guid id)
    {
        return Path.Combine(Shared.RevertDirectory, $"{id}.json");
    }

    private static async Task<RevertData?> LoadAsync(string path)
    {
        if (!File.Exists(path))
            return null;

        // Static reads are inherently safe with atomic writes (WriteJsonAtomicAsync uses temp+replace),
        // so we avoid allocating a SemaphoreSlim in the global dictionary for read-only operations.
        // This prevents unbounded dictionary growth from one-off reads that never call RemoveRevertData.
        return await LoadAsyncUnlocked(path);
    }

    private static async Task<RevertData?> LoadAsyncUnlocked(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            for (var i = 0; i < 3; i++)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(path);
                    if (string.IsNullOrWhiteSpace(json))
                        return null;

                    return JsonConvert.DeserializeObject<RevertData>(json);
                }
                catch (IOException) when (i < 2)
                {
                    await Task.Delay(50);
                }
            }
            return null;
        }
        catch (JsonException ex)
        {
            TraceCorruptRevertFile(path, ex);
            return null;
        }
        catch (Exception ex)
        {
            TraceCorruptRevertFile(path, ex);
            return null;
        }
    }

    private static Guid? ExtractIdFromPath(string path)
    {
        try
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (Guid.TryParse(fileName, out var id))
                return id;
        }
        catch { }
        return null;
    }

    private static void TraceCorruptRevertFile(string path, Exception ex)
    {
        global::System.Diagnostics.Trace.TraceWarning(
            "Corrupt revert file {0}: {1}",
            path,
            ex.Message
        );
    }

    public void RemoveRevertData(Guid id, string? name = null)
    {
        Remove(id, name);
        if (_fileLocks.TryRemove(id, out var sem))
        {
            sem.Dispose();
        }
    }

    private async Task WriteJsonAsync(Guid optimizationId, string path, RevertData data)
    {
        data.SchemaVersion = SchemaVersion;
        var lockObj = await AcquireFileLockAsync(optimizationId);
        try
        {
            await WriteJsonAtomicAsync(path, data);
            var totalOperations = data.Steps.Count(s => s != null);
            _logger.LogInformation("Saved {Total} operations to {Path}", totalOperations, path);
        }
        finally
        {
            lockObj.Release();
        }
    }

    private static async Task WriteJsonAtomicAsync(string path, RevertData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        var tempPath = path + ".tmp";
        await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);

        if (File.Exists(path))
            File.Replace(tempPath, path, destinationBackupFileName: null);
        else
            File.Move(tempPath, path);
    }

    private static async Task<SemaphoreSlim> AcquireFileLockAsync(Guid id)
    {
        var lockObj = _fileLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        if (
            !await lockObj
                .WaitAsync(TimeSpan.FromSeconds(FileLockTimeoutSeconds))
                .ConfigureAwait(false)
        )
            throw new TimeoutException(
                string.Format("Timed out waiting for revert file lock ({0}).", id)
            );

        return lockObj;
    }

    private async Task<List<(int Index, IRevertStep Step)>> LoadStepsAsync(Guid id)
    {
        var data = await LoadAsync(GetFilePath(id));
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
                _logger.LogWarning(
                    "Skipped unknown revert step type '{Type}' for optimization {Id}",
                    stepData.Type,
                    id
                );
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
            try
            {
                var instance = (IRevertStep)Activator.CreateInstance(type)!;
                var method = type.GetMethod("FromData", BindingFlags.Static | BindingFlags.Public);
                if (method != null)
                    dict[instance.Type] = data => (IRevertStep)method.Invoke(null, [data])!;
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Trace.TraceWarning(
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
}
