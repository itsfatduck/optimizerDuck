using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Optimization;
using optimizerDuck.Core.Models.Revert;
using optimizerDuck.Core.Models.Revert.Steps;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Resources.Languages;

namespace optimizerDuck.Services.Managers;

/// <summary>
///     Manages revert functionality for optimizations, allowing users to undo changes.
/// </summary>
public class RevertManager(ILogger<RevertManager> logger)
{
    /// <summary>
    ///     Async local storage for the current revert context.
    /// </summary>
    private static readonly AsyncLocal<RevertContext?> _current = new();

    /// <summary>
    ///     Auto-discovered registry mapping step type strings to their FromData factory methods.
    ///     Built once at startup via reflection.
    /// </summary>
    private static readonly Dictionary<string, Func<JObject, IRevertStep>> _stepRegistry = BuildStepRegistry();

    /// <summary>
    ///     Gets the current revert context for the active async operation.
    /// </summary>
    public static RevertContext? Current => _current.Value;

    /// <summary>
    ///     Begins recording revert steps for the specified optimization.
    /// </summary>
    /// <param name="optimization">The optimization being performed.</param>
    /// <returns>The created revert context.</returns>
    public RevertContext BeginRecording(IOptimization optimization)
    {
        var context = new RevertContext(optimization, logger, this);
        _current.Value = context;
        return context;
    }

    /// <summary>
    ///     Records a revert step to the current context.
    /// </summary>
    /// <param name="step">The revert step to record.</param>
    public static void Record(IRevertStep step)
    {
        Current?.AddStep(step);
    }

    internal async Task SaveAsync(Guid optimizationId, string optimizationName, Stack<IRevertStep> steps)
    {
        var filePath = Path.Combine(Shared.RevertDirectory, optimizationId + ".json");
        var tempPath = filePath + ".tmp";

        try
        {
            // Persist revert steps atomically to avoid partial writes.
            var stepData = steps.Reverse()
                .Select(s => new RevertStepData
                {
                    Type = s.Type,
                    Data = s.ToData()
                })
                .ToList();

            var payload = new RevertData
            {
                OptimizationId = optimizationId,
                OptimizationName = optimizationName,
                AppliedAt = DateTime.Now,
                Steps = stepData
            };

            Directory.CreateDirectory(Shared.RevertDirectory);
            var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, filePath, true);
            logger.LogInformation("Saved {Count} revert steps for {Name} to {File}", steps.Count, optimizationName,
                filePath);
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // Best-effort cleanup only.
            }

            logger.LogError(ex, "Failed to save revert data for {Name}", optimizationName);
            throw;
        }
    }

    public async Task<RevertResult> RevertAsync(Guid optimizationId, string optimizationKey,
        IProgress<ProcessingProgress>? progress = null, int maxRetries = 1)
    {
        try
        {
            var validation = await ValidateAsync(optimizationId);
            if (!validation.IsValid)
            {
                logger.LogWarning("Invalid revert data for {Name}: {Message}", optimizationKey, validation.Message);
                return new RevertResult
                {
                    Success = false,
                    Message = string.Format(Translations.Revert_Error_InvalidData, optimizationKey,
                        validation.LocalizedMessage)
                };
            }

            var steps = await LoadStepsAsync(optimizationId);
            if (steps.Count == 0)
            {
                logger.LogWarning("No revert steps found for {Name}", optimizationKey);
                return new RevertResult
                {
                    Success = false,
                    Message = string.Format(Translations.Revert_Error_NoDataFound, optimizationKey)
                };
            }

            logger.LogInformation("Reverting {Name} with {Count} steps (maxRetries={MaxRetries})",
                optimizationKey, steps.Count, maxRetries);

            var failedStepDetails = new List<OperationStepResult>();
            var totalSteps = steps.Count;
            var stack = new Stack<RevertStepEntry>(steps);

            while (stack.Count > 0)
            {
                var (currentIndex, step) = stack.Pop();

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

                // Retry loop: attempt up to maxRetries + 1 times total
                for (var attempt = 0; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        success = await step.ExecuteAsync();
                        if (success) break;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                    }

                    if (attempt < maxRetries)
                    {
                        logger.LogWarning("Revert step {Index} ({Type}) failed on attempt {Attempt}, retrying...",
                            currentIndex, step.Type, attempt + 1);
                        await Task.Delay(200 * (attempt + 1)); // progressive delay
                    }
                }

                if (!success)
                {
                    logger.LogError(lastError, "Revert step {Index} ({Type}) failed after {Attempts} attempt(s)",
                        currentIndex, step.Type, maxRetries + 1);

                    failedStepDetails.Add(new OperationStepResult
                    {
                        Index = currentIndex,
                        Name = step.Type,
                        Description = $"Revert step #{currentIndex}",
                        Success = false,
                        Error = lastError?.Message ?? Translations.Revert_Error_StepFailed,
                        RetryAction = async () => await step.ExecuteAsync()
                    });
                }
            }

            var failedCount = failedStepDetails.Count;
            var allFailed = failedCount == totalSteps;
            var hasFailures = failedCount > 0;

            // Partial revert: save remaining failed steps back to file for later retry
            if (hasFailures && !allFailed)
            {
                await SaveFailedStepsAsync(optimizationId, optimizationKey, steps, failedStepDetails);
                logger.LogWarning("Partial revert for {Name}: {Failed}/{Total} steps failed, saved for later retry",
                    optimizationKey, failedCount, totalSteps);
            }
            else if (!hasFailures)
            {
                await RemoveAsync(optimizationId, optimizationKey);
            }

            return new RevertResult
            {
                Success = !hasFailures,
                Message = allFailed
                    ? string.Format(Translations.Optimization_Revert_Error_Failed, optimizationKey)
                    : hasFailures
                        ? string.Format(Translations.Optimization_Revert_Error_FailedWithSteps, optimizationKey,
                            failedCount)
                        : string.Format(Translations.Optimization_Revert_Success, optimizationKey),
                FailedStepDetails = failedStepDetails
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to revert {Name}", optimizationKey);
            return new RevertResult
            {
                Success = false,
                Message = string.Format(Translations.Revert_Error_RevertFailed, optimizationKey, ex.Message),
                Exception = ex
            };
        }
    }

    /// <summary>
    ///     Saves only the failed steps back to the revert file so a subsequent revert can retry them.
    /// </summary>
    private async Task SaveFailedStepsAsync(Guid optimizationId, string optimizationName,
        List<RevertStepEntry> allSteps, List<OperationStepResult> failedDetails)
    {
        var filePath = Path.Combine(Shared.RevertDirectory, optimizationId + ".json");
        var tempPath = filePath + ".tmp";

        try
        {
            var failedIndices = new HashSet<int>(failedDetails.Select(f => f.Index));
            var remainingSteps = allSteps
                .Where(s => failedIndices.Contains(s.Index))
                .Select(s => new RevertStepData
                {
                    Type = s.Step.Type,
                    Data = s.Step.ToData()
                })
                .ToList();

            var payload = new RevertData
            {
                OptimizationId = optimizationId,
                OptimizationName = optimizationName,
                AppliedAt = DateTime.Now,
                Steps = remainingSteps
            };

            Directory.CreateDirectory(Shared.RevertDirectory);
            var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, filePath, true);
            logger.LogInformation("Updated revert file for {Name} with {Count} remaining failed steps",
                optimizationName, remainingSteps.Count);
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // Best-effort cleanup only.
            }

            logger.LogError(ex, "Failed to save remaining failed steps for {Name}", optimizationName);
        }
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
        // Revert data file existence is the source of truth.
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

    private Task RemoveAsync(Guid optimizationId, string optimizationName)
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

        return Task.CompletedTask;
    }

    #region Auto-Discovery

    /// <summary>
    ///     Builds the step registry by scanning all IRevertStep implementations via reflection.
    ///     Each step type must have a static FromData(JObject) method.
    /// </summary>
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
            {
                try
                {
                    // Create a temporary instance to get the Type property value
                    var instance = (IRevertStep)Activator.CreateInstance(type)!;
                    var typeKey = instance.Type;

                    // Find the static FromData method (accepts JObject or JToken)
                    var fromDataMethod = type.GetMethod("FromData",
                        BindingFlags.Static | BindingFlags.Public,
                        null,
                        [typeof(JObject)],
                        null);

                    // Fallback: try JToken parameter
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
                    // Skip types that can't be instantiated or lack FromData
                }
            }
        }
        catch
        {
            // Fallback: if reflection fails entirely, the registry remains empty
            // and DeserializeStep will return null for all types.
        }

        return registry;
    }

    /// <summary>
    ///     Returns the list of registered revert step type names.
    /// </summary>
    public static IReadOnlyCollection<string> RegisteredStepTypes => _stepRegistry.Keys.ToList().AsReadOnly();

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

    #endregion Auto-Discovery

    #region Validation

    private async Task<RevertValidationResult> ValidateAsync(Guid optimizationId)
    {
        var filePath = Path.Combine(Shared.RevertDirectory, optimizationId + ".json");
        if (!File.Exists(filePath))
            return RevertValidationResult.Fail(Translations.Revert_Error_FileNotFound,
                "Invalid revert data file not found.");

        string content;
        try
        {
            content = await File.ReadAllTextAsync(filePath);
            if (string.IsNullOrWhiteSpace(content))
                return RevertValidationResult.Fail(Translations.Revert_Error_FileEmpty,
                    "Invalid revert data file is empty.");
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
                return RevertValidationResult.Fail(Translations.Revert_Error_InvalidJson, "Invalid revert data JSON.");
        }
        catch (Exception ex)
        {
            return RevertValidationResult.Fail(
                string.Format(Translations.Revert_Error_InvalidJsonFormat, ex.Message), ex.Message);
        }

        if (data.OptimizationId != optimizationId)
            return RevertValidationResult.Fail(Translations.Revert_Error_OptimizationIdMismatch,
                "Invalid optimization ID.");

        if (data.Steps.Count == 0)
            return RevertValidationResult.Fail(Translations.Revert_Error_NoSteps, "No revert steps found.");

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
                string.Join(", ", invalidSteps)), "Invalid revert steps.");

        return RevertValidationResult.Success();
    }

    #endregion Validation

    internal static void ClearCurrent()
    {
        _current.Value = null;
    }

    public static void ClearAllRevertDataAsync()
    {
        if (!Directory.Exists(Shared.RevertDirectory))
            return;

        foreach (var filePath in Directory.GetFiles(Shared.RevertDirectory))
            try
            {
                File.Delete(filePath);
            }
            catch
            {
                // Best-effort cleanup only.
            }
    }

    private sealed record RevertStepEntry(int Index, IRevertStep Step);
}