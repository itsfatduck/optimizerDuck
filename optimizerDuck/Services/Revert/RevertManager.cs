using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Revert;
using optimizerDuck.Domain.UI;
using optimizerDuck.Resources.Languages;

namespace optimizerDuck.Services.Managers;

public class RevertManager(ILogger<RevertManager> _logger, ILoggerFactory _loggerFactory)
{
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

        await WriteJsonAsync(
            GetFilePath(scope.OptimizationId!.Value),
            new RevertData
            {
                OptimizationId = scope.OptimizationId!.Value,
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

        operationLogger.LogDebug("Reverting steps in reverse order (total: {Total})", total);

        for (var i = 0; i < total; i++)
        {
            var (idx, step) = sortedSteps[i];
            progress?.Report(
                new ProcessingProgress
                {
                    Message = string.Format(
                        Translations.Optimization_Revert_ExecutingStep,
                        idx - 1,
                        total,
                        step.Type
                    ),
                    Value = i + 1,
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
                failedSteps.Add(
                    new OperationStepResult
                    {
                        Index = idx,
                        Name = step.Type,
                        Description = step.Description,
                        Success = false,
                        Error = ex.Message,
                        RetryAction = () => step.ExecuteAsync(),
                    }
                );
            }
        }

        if (failedSteps.Count < total)
            RemoveRevertData(optimization.Id, optimization.OptimizationKey);

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
        var lockObj = _fileLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));

        await lockObj.WaitAsync(TimeSpan.FromSeconds(30));
        try
        {
            var data =
                await LoadAsync(filePath)
                ?? new RevertData
                {
                    OptimizationId = id,
                    OptimizationName = name,
                    Steps = Array.Empty<RevertStepData?>(),
                };

            // Expand array if needed
            if (data.Steps.Length < stepIndex)
            {
                var newSteps = new RevertStepData?[stepIndex];
                Array.Copy(data.Steps, newSteps, data.Steps.Length);
                data.Steps = newSteps;
            }

            // Place step at correct position (0-based index)
            data.Steps[stepIndex - 1] = new RevertStepData
            {
                Index = stepIndex,
                Type = step.Type,
                Data = step.ToData(),
            };

            await WriteJsonAsync(filePath, data);
        }
        finally
        {
            lockObj.Release();
        }
    }

    #region Helpers

    public static Task<bool> IsAppliedAsync(Guid id)
    {
        return Task.FromResult(File.Exists(GetFilePath(id)));
    }

    public static async Task<RevertData?> GetRevertDataAsync(Guid id)
    {
        return await LoadAsync(GetFilePath(id));
    }

    public static void ClearAllRevertData(ILogger logger)
    {
        if (!Directory.Exists(Shared.RevertDirectory))
            return;
        foreach (var f in Directory.GetFiles(Shared.RevertDirectory))
            try
            {
                File.Delete(f);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete {File}", f);
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
        try
        {
            return JsonConvert.DeserializeObject<RevertData>(await File.ReadAllTextAsync(path));
        }
        catch
        {
            return null;
        }
    }

    public void RemoveRevertData(Guid id, string? name = null)
    {
        Remove(id, name);
    }

    private async Task WriteJsonAsync(string path, RevertData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        await using var fileStream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true
        );
        await using var writer = new StreamWriter(fileStream);
        await writer.WriteAsync(json);

        var totalOperations = data.Steps.Count(s => s != null);
        _logger.LogInformation("Saved {Total} operations to {Path}", totalOperations, path);
    }

    private async Task<List<(int Index, IRevertStep Step)>> LoadStepsAsync(Guid id)
    {
        var data = await LoadAsync(GetFilePath(id));
        if (data == null || data.Steps.Length == 0)
            return [];

        // Validate indexes: must be > 0, no duplicates
        var seenIndexes = new HashSet<int>();
        foreach (var step in data.Steps.Where(s => s != null))
        {
            if (step!.Index <= 0)
            {
                _logger.LogWarning("Invalid index {Index} in revert data for {Id}", step.Index, id);
                return [];
            }
            if (!seenIndexes.Add(step.Index))
            {
                _logger.LogWarning(
                    "Duplicate index {Index} in revert data for {Id}",
                    step.Index,
                    id
                );
                return [];
            }
        }

        var result = new List<(int, IRevertStep)>();
        foreach (var stepData in data.Steps.Where(s => s != null))
        {
            var step = DeserializeStep(stepData!.Type, stepData.Data);
            if (step != null)
                result.Add((stepData.Index, step)); // Use explicit Index
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
                _logger.LogInformation("Removed revert data for {Name}", name ?? id.ToString());
            }
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
            try
            {
                var instance = (IRevertStep)Activator.CreateInstance(type)!;
                var method = type.GetMethod("FromData", BindingFlags.Static | BindingFlags.Public);
                if (method != null)
                    dict[instance.Type] = data => (IRevertStep)method.Invoke(null, [data])!;
            }
            catch { }

        return dict;
    }

    private IRevertStep? DeserializeStep(string type, JToken data)
    {
        if (data is not JObject obj)
            return null;
        return _stepRegistry.Value.TryGetValue(type, out var factory) ? factory(obj) : null;
    }

    #endregion
}
