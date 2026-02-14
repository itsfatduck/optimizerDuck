using System.IO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Revert;
using optimizerDuck.Core.Models.Revert.Steps;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Resources.Languages;

namespace optimizerDuck.Services.Managers;

public class RevertManager(ILogger<RevertManager> logger)
{
    private static readonly AsyncLocal<RevertContext?> _current = new();

    public static RevertContext? Current => _current.Value;

    public RevertContext BeginRecording(IOptimization optimization)
    {
        var context = new RevertContext(optimization, logger, this);
        _current.Value = context;
        return context;
    }

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
        IProgress<ProcessingProgress>? progress = null)
    {
        // Load revert data, validate, then execute steps in reverse order.
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

            logger.LogInformation("Reverting {Name} with {Count} steps", optimizationKey, steps.Count);

            var failedSteps = 0;
            var stack = new Stack<RevertStepEntry>(steps);

            while (stack.Count > 0)
            {
                var (currentIndex, step) = stack.Pop();

                progress?.Report(new ProcessingProgress
                {
                    Message = string.Format(Translations.Optimization_Revert_ExecutingStep, currentIndex, steps.Count,
                        step.Type),
                    IsIndeterminate = false,
                    Value = currentIndex,
                    Total = steps.Count
                });

                var success = await step.ExecuteAsync();
                if (!success) failedSteps++;
            }

            var allFailed = failedSteps == steps.Count;
            var hasFailures = failedSteps > 0;

            if (!hasFailures)
                await RemoveAsync(optimizationId, optimizationKey);

            return new RevertResult
            {
                Success = !hasFailures,
                Message = allFailed
                    ? string.Format(Translations.Optimization_Revert_Error_Failed, optimizationKey)
                    : hasFailures
                        ? string.Format(Translations.Optimization_Revert_Error_FailedWithSteps, optimizationKey,
                            failedSteps)
                        : string.Format(Translations.Optimization_Revert_Success, optimizationKey)
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

    private async Task<List<RevertStepEntry>> LoadStepsAsync(Guid optimizationId)
    {
        var filePath = Path.Combine(Shared.RevertDirectory, optimizationId + ".json");
        if (!File.Exists(filePath))
            return new List<RevertStepEntry>();

        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            if (string.IsNullOrWhiteSpace(content))
                return new List<RevertStepEntry>();

            var revertData = JsonConvert.DeserializeObject<RevertData>(content);
            if (revertData == null || revertData.Steps.Count == 0)
                return new List<RevertStepEntry>();

            var stepsList = new List<RevertStepEntry>();
            var index = 0;
            foreach (var stepData in revertData.Steps)
            {
                var step = DeserializeStep(stepData.Type, stepData.Data);
                if (step == null)
                    throw new InvalidDataException($"Invalid revert step type '{stepData.Type}'.");
                index++;
                stepsList.Add(new RevertStepEntry(index, step));
            }

            return stepsList;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load revert steps for {Id}", optimizationId);
            return new List<RevertStepEntry>();
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

            var revertData = JsonConvert.DeserializeObject<RevertData>(content);
            return revertData;
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

            return serviceType switch
            {
                "Registry" => RegistryRevertStep.FromData(obj),
                "Service" => ServiceRevertStep.FromData(obj),
                "Shell" => ShellRevertStep.FromData(obj),
                _ => null
            };
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