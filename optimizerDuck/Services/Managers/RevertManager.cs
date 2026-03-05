using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Execution;
using optimizerDuck.Core.Models.Optimization;
using optimizerDuck.Core.Models.Revert;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Resources.Languages;

namespace optimizerDuck.Services.Managers;

public class RevertManager(ILogger<RevertManager> logger)
{
    private static readonly Dictionary<string, Func<JObject, IRevertStep>> _stepRegistry = BuildStepRegistry();
    private static readonly ConcurrentDictionary<Guid, object> _fileLocks = new();

    public static IReadOnlyCollection<string> RegisteredStepTypes => _stepRegistry.Keys.ToList().AsReadOnly();

    public async Task SaveRevertDataAsync(ExecutionScope executionScope)
    {
        var successfulSteps = executionScope.SuccessfulSteps
            .Where(s => s.RevertStep != null)
            .ToList();

        if (successfulSteps.Count == 0)
        {
            logger.LogWarning("No revert steps to save for {Key}", executionScope.OptimizationKey);
            return;
        }

        var steps = successfulSteps
            .Select(s => new RevertStepData
            {
                Type = s.RevertStep!.Type,
                Data = s.RevertStep.ToData()
            })
            .ToList();

        var filePath = Path.Combine(Shared.RevertDirectory, executionScope.OptimizationId + ".json");
        var tempPath = filePath + ".tmp";

        try
        {
            var payload = new RevertData
            {
                OptimizationId = executionScope.OptimizationId,
                OptimizationName = executionScope.OptimizationKey,
                AppliedAt = DateTime.Now,
                Steps = steps
            };

            Directory.CreateDirectory(Shared.RevertDirectory);
            var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, filePath, true);
            logger.LogInformation("Saved {Count} revert steps for {Name} to {File}",
                steps.Count, executionScope.OptimizationKey, filePath);
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (Exception deleteEx)
            {
                logger.LogWarning(deleteEx, "Failed to delete temp file {File} after save failure", tempPath);
            }

            logger.LogError(ex, "Failed to save revert data for {Name}", executionScope.OptimizationKey);
            throw;
        }
    }

    public async Task<RevertResult> RevertAsync(IOptimization optimization,
        IProgress<ProcessingProgress>? progress = null)
    {
        using var scope = ExecutionScope.BeginForLogging(optimization.Id, optimization.OptimizationKey, logger);

        try
        {
            var validation = await ValidateAsync(optimization.Id);
            if (!validation.IsValid)
            {
                logger.LogWarning("Invalid revert data for {Key}: {Message}", optimization.OptimizationKey,
                    validation.Message);
                return new RevertResult
                {
                    Success = false,
                    Message = string.Format(Translations.Revert_Error_InvalidData, optimization.Name,
                        validation.LocalizedMessage)
                };
            }

            var steps = await LoadStepsAsync(optimization.Id);
            if (steps.Count == 0)
            {
                logger.LogWarning("No revert steps found for {Key}", optimization.OptimizationKey);
                return new RevertResult
                {
                    Success = false,
                    Message = string.Format(Translations.Revert_Error_NoDataFound, optimization.Name)
                };
            }

            logger.LogInformation("Reverting {Key} ({StepCount} steps)",
                optimization.OptimizationKey, steps.Count);

            var failedStepDetails = new List<OperationStepResult>();
            var totalSteps = steps.Count;

            // Revert steps in reverse order (LIFO) for correct undo semantics
            for (var i = steps.Count - 1; i >= 0; i--)
            {
                var (currentIndex, step) = steps[i];

                progress?.Report(new ProcessingProgress
                {
                    Message = string.Format(Translations.Optimization_Revert_ExecutingStep, currentIndex, totalSteps,
                        step.Type),
                    IsIndeterminate = false,
                    Value = currentIndex,
                    Total = totalSteps
                });

                var success = false;
                Exception? lastError = null;

                try
                {
                    success = await step.ExecuteAsync();
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }

                if (!success)
                {
                    var errorMessage = lastError?.Message ?? Translations.Revert_Error_StepFailed;
                    logger.LogError(lastError, "Revert step {Index} ({Type}) failed: {Message}",
                        currentIndex, step.Type, errorMessage);

                    failedStepDetails.Add(new OperationStepResult
                    {
                        Index = currentIndex,
                        Name = step.Type,
                        Description = step.Description,
                        Success = false,
                        Error = errorMessage,
                        RetryAction = async () => await step.ExecuteAsync()
                    });
                }
            }

            var failedCount = failedStepDetails.Count;
            var allFailed = failedCount == totalSteps;
            var hasFailures = failedCount > 0;

            // if some steps failed but not all, log a warning
            if (hasFailures && !allFailed)
                logger.LogWarning(
                    "Partial revert for {Key}: {FailedCount}/{TotalCount} steps failed. " +
                    "Revert data file will be removed; remaining retries operate in-memory.",
                    optimization.OptimizationKey, failedCount, totalSteps);

            // keep revert data if its all failed
            if (!allFailed) Remove(optimization.Id, optimization.Name);

            return new RevertResult
            {
                Success = !hasFailures,
                IsCompleteFailure = allFailed,
                Message = allFailed
                    ? string.Format(Translations.Optimization_Revert_Error_Failed, optimization.Name)
                    : hasFailures
                        ? string.Format(Translations.Optimization_Revert_Error_FailedWithSteps, optimization.Name,
                            failedCount)
                        : string.Format(Translations.Optimization_Revert_Success, optimization.Name),
                FailedStepDetails = failedStepDetails
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to revert {Key}", optimization.OptimizationKey);
            return new RevertResult
            {
                Success = false,
                IsCompleteFailure = true,
                Message = string.Format(Translations.Revert_Error_RevertFailed, optimization.Name, ex.Message),
                Exception = ex
            };
        }
    }

    public Task AppendOrUpdateRevertStepAsync(Guid optimizationId, string optimizationName, int stepIndex,
        IRevertStep newStep)
    {
        var filePath = Path.Combine(Shared.RevertDirectory, optimizationId + ".json");
        var tempPath = filePath + ".tmp";

        // Use a per-file lock keyed by optimizationId to ensure thread-safe file access
        var fileLockObj = _fileLocks.GetOrAdd(optimizationId, _ => new object());
        lock (fileLockObj)
        {
            try
            {
                var content = File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;
                var revertData = string.IsNullOrWhiteSpace(content)
                    ? new RevertData
                    {
                        OptimizationId = optimizationId,
                        OptimizationName = optimizationName,
                        AppliedAt = DateTime.Now,
                        Steps = []
                    }
                    : JsonConvert.DeserializeObject<RevertData>(content);

                if (revertData == null)
                    return Task.CompletedTask;

                // Adjust the steps list to match exactly the 1-based index length
                while (revertData.Steps.Count < stepIndex)
                    revertData.Steps.Add(new RevertStepData { Type = "Unknown", Data = new JObject() });

                // Replace at exact 0-based index
                revertData.Steps[stepIndex - 1] = new RevertStepData
                {
                    Type = newStep.Type,
                    Data = newStep.ToData()
                };

                Directory.CreateDirectory(Shared.RevertDirectory);
                var json = JsonConvert.SerializeObject(revertData, Formatting.Indented);
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, filePath, true);

                logger.LogInformation("Successfully inserted retried revert step at index {Index} for {Name}",
                    stepIndex, optimizationName);
            }
            catch (Exception ex)
            {
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch (Exception deleteEx)
                {
                    logger.LogWarning(deleteEx, "Failed to delete temp file {File} after save failure", tempPath);
                }

                logger.LogError(ex, "Failed to append/update revert step {Index} for {Name}", stepIndex,
                    optimizationName);
            }
        }

        return Task.CompletedTask;
    }

    private async Task<List<RevertStepEntry>> LoadStepsAsync(Guid optimizationId)
    {
        var filePath = Path.Combine(Shared.RevertDirectory, optimizationId + ".json");
        if (!File.Exists(filePath))
            return [];

        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            if (string.IsNullOrWhiteSpace(content))
                return [];

            var revertData = JsonConvert.DeserializeObject<RevertData>(content);
            if (revertData == null || revertData.Steps.Count == 0)
                return [];

            var stepsList = new List<RevertStepEntry>();
            var index = 0;
            foreach (var stepData in revertData.Steps)
            {
                var step = DeserializeStep(stepData.Type, stepData.Data);
                if (step == null)
                {
                    logger.LogWarning("Skipping unknown revert step type '{Type}' at index {Index}",
                        stepData.Type, index);
                    continue;
                }

                index++;
                stepsList.Add(new RevertStepEntry(index, step));
            }

            return stepsList;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load revert steps for {Id}", optimizationId);
            return [];
        }
    }

    public static Task<bool> IsAppliedAsync(Guid optimizationId)
    {
        var filePath = Path.Combine(Shared.RevertDirectory, optimizationId + ".json");
        return Task.FromResult(File.Exists(filePath));
    }

    public static async Task<RevertData?> GetRevertDataAsync(Guid optimizationId)
    {
        var filePath = Path.Combine(Shared.RevertDirectory, optimizationId + ".json");
        if (!File.Exists(filePath))
            return null;

        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            if (string.IsNullOrWhiteSpace(content))
                return null;

            return JsonConvert.DeserializeObject<RevertData>(content);
        }
        catch
        {
            return null;
        }
    }

    private void Remove(Guid optimizationId, string optimizationName)
    {
        try
        {
            var filePath = Path.Combine(Shared.RevertDirectory, optimizationId + ".json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                logger.LogInformation("Removed revert data for {Name} ({File})", optimizationName, filePath);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove revert data for {Id}", optimizationId);
        }
    }

    private static Dictionary<string, Func<JObject, IRevertStep>> BuildStepRegistry()
    {
        var registry = new Dictionary<string, Func<JObject, IRevertStep>>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var stepTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false }
                            && typeof(IRevertStep).IsAssignableFrom(t));

            foreach (var type in stepTypes)
                try
                {
                    var instance = (IRevertStep)Activator.CreateInstance(type)!;
                    var typeKey = instance.Type;

                    var fromDataMethod = type.GetMethod("FromData",
                        BindingFlags.Static | BindingFlags.Public,
                        null,
                        [typeof(JObject)],
                        null);

                    fromDataMethod ??= type.GetMethod("FromData",
                        BindingFlags.Static | BindingFlags.Public,
                        null,
                        [typeof(JToken)],
                        null);

                    if (fromDataMethod != null)
                        registry[typeKey] = data =>
                            (IRevertStep)fromDataMethod.Invoke(null, [data])!;
                }
                catch
                {
                }
        }
        catch
        {
        }

        return registry;
    }

    private IRevertStep? DeserializeStep(string serviceType, JToken data)
    {
        try
        {
            if (data is not JObject obj)
            {
                logger.LogError(
                    "Invalid revert step payload for type {Type}. Expected JObject, got {TokenType}",
                    serviceType,
                    data.Type
                );
                return null;
            }

            if (_stepRegistry.TryGetValue(serviceType, out var factory))
                return factory(obj);

            logger.LogWarning("Unknown revert step type '{Type}'. Registered types: {Types}",
                serviceType, string.Join(", ", _stepRegistry.Keys));
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize step of type {Type}", serviceType);
            return null;
        }
    }

    private async Task<RevertValidationResult> ValidateAsync(Guid optimizationId)
    {
        var filePath = Path.Combine(Shared.RevertDirectory, optimizationId + ".json");
        if (!File.Exists(filePath))
            return RevertValidationResult.Fail(Translations.Revert_Error_FileNotFound,
                "Revert data file not found.");

        string content;
        try
        {
            content = await File.ReadAllTextAsync(filePath);
            if (string.IsNullOrWhiteSpace(content))
                return RevertValidationResult.Fail(Translations.Revert_Error_FileEmpty,
                    "Revert data file is empty.");
        }
        catch (Exception ex)
        {
            return RevertValidationResult.Fail(string.Format(Translations.Revert_Error_ReadFailed, ex.Message),
                ex.Message);
        }

        RevertData? data;
        try
        {
            data = JsonConvert.DeserializeObject<RevertData>(content);
            if (data == null)
                return RevertValidationResult.Fail(Translations.Revert_Error_InvalidJson, "Revert data JSON is null after deserialization.");
        }
        catch (Exception ex)
        {
            return RevertValidationResult.Fail(
                string.Format(Translations.Revert_Error_InvalidJsonFormat, ex.Message), ex.Message);
        }

        if (data.OptimizationId != optimizationId)
            return RevertValidationResult.Fail(Translations.Revert_Error_OptimizationIdMismatch,
                $"Optimization ID mismatch: expected {optimizationId}, found {data.OptimizationId}.");

        if (data.Steps.Count == 0)
            return RevertValidationResult.Fail(Translations.Revert_Error_NoSteps, "Revert data contains zero steps.");

        var invalidSteps = new List<string>();
        foreach (var stepData in data.Steps)
        {
            if (string.IsNullOrWhiteSpace(stepData.Type))
            {
                invalidSteps.Add("(empty)");
                continue;
            }

            if (!_stepRegistry.ContainsKey(stepData.Type))
            {
                invalidSteps.Add(stepData.Type);
                continue;
            }

            var step = DeserializeStep(stepData.Type, stepData.Data);
            if (step == null)
                invalidSteps.Add(stepData.Type);
        }

        if (invalidSteps.Count > 0)
            return RevertValidationResult.Fail(string.Format(Translations.Revert_Error_InvalidSteps,
                string.Join(", ", invalidSteps)), $"Invalid or unrecognized step types: {string.Join(", ", invalidSteps)}.");

        return RevertValidationResult.Success();
    }

    public static void ClearAllRevertData(ILogger logger)
    {
        if (!Directory.Exists(Shared.RevertDirectory))
            return;

        foreach (var filePath in Directory.GetFiles(Shared.RevertDirectory))
            try
            {
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete revert data file {File}", filePath);
            }
    }

    public sealed record RevertStepEntry(int Index, IRevertStep Step);
}