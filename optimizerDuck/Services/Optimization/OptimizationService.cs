using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Revert;
using optimizerDuck.Domain.UI;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Managers;
using optimizerDuck.Services.OptimizationServices;
using optimizerDuck.UI.ViewModels.Dialogs;
using optimizerDuck.UI.Dialogs;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.Services;

public class OptimizationService(
    RevertManager revertManager,
    ILoggerFactory loggerFactory,
    SystemInfoService systemInfoService,
    StreamService streamService,
    IContentDialogService contentDialogService,
    ILogger<OptimizationService> logger)
{
    private readonly ILogger _logger = logger;

    public bool WasRequestedRestorePoint { get; set; } = false;

    public async Task<RestorePointResult> CreateRestorePointAsync()
    {
        var dialogViewModel = new ProcessingViewModel();
        var dialogContent = new ProcessingDialog { DataContext = dialogViewModel };

        var dialog = new ContentDialog
        {
            Title = Translations.RestorePoint_Title,
            Content = dialogContent,
            IsFooterVisible = false
        };

        _ = contentDialogService.ShowAsync(dialog, CancellationToken.None);

        try
        {
            dialogViewModel.ProgressReporter.Report(new ProcessingProgress
            {
                Message = Translations.RestorePoint_Progress_Creating,
                IsIndeterminate = true
            });

            using var scope = ExecutionScope.BeginForLogging(_logger);

            var result = await ShellService.PowerShellAsync(
                $"Checkpoint-Computer -Description \"{Shared.RestorePointName}\" -RestorePointType MODIFY_SETTINGS");

            if (result.Stderr.Contains("already been created within the past",
                    StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Restore point creation skipped: frequency limit reached.");
                return RestorePointResult.FrequencyLimitReached;
            }

            if (result.ExitCode == 0)
                return RestorePointResult.Success;

            if (!Regex.IsMatch(result.Stderr,
                    @"\b(is\s+disabled|system\s+restore\s+is\s+disabled|disabled\s+by\s+group\s+policy|disableconfig|disablesr|protection\s+is\s+off)\b",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                _logger.LogError("Failed to create restore point: {Message}", result.Stderr);
                return RestorePointResult.Failed;
            }

            _logger.LogInformation("Enabling System Protection...");
            dialogViewModel.ProgressReporter.Report(new ProcessingProgress
                { Message = Translations.RestorePoint_Progress_Enabling, IsIndeterminate = true });

            var enableResult = await ShellService.PowerShellAsync("Enable-ComputerRestore -Drive \"$env:SystemDrive\"");
            if (enableResult.ExitCode != 0)
            {
                _logger.LogError("Failed to enable System Protection: {Message}", enableResult.Stderr);
                return RestorePointResult.Failed;
            }

            dialogViewModel.ProgressReporter.Report(new ProcessingProgress
                { Message = Translations.RestorePoint_Progress_Retrying, IsIndeterminate = true });

            result = await ShellService.PowerShellAsync(
                $"Checkpoint-Computer -Description \"{Shared.RestorePointName}\" -RestorePointType MODIFY_SETTINGS");

            if (result.Stderr.Contains("already been created within the past", StringComparison.OrdinalIgnoreCase))
                return RestorePointResult.FrequencyLimitReached;

            return result.ExitCode == 0 ? RestorePointResult.Success : RestorePointResult.Failed;
        }
        finally
        {
            dialog.Hide();
        }
    }

    public async Task<OptimizationResult> ApplyAsync(IOptimization optimization, IProgress<ProcessingProgress> progress)
    {
        var optLogger = loggerFactory.CreateLogger(optimization.GetType());
        using var scope = ExecutionScope.Begin(optimization, optLogger);

        progress.Report(new ProcessingProgress
            { Message = Translations.Optimization_Apply_Processing, IsIndeterminate = true });

        try
        {
            var applyResult = await Task.Run(async () =>
                await optimization.ApplyAsync(progress, new OptimizationContext
                {
                    Logger = optLogger,
                    Snapshot = systemInfoService.Snapshot,
                    StreamService = streamService
                }));

            if (!string.IsNullOrWhiteSpace(applyResult.Message))
                return new OptimizationResult
                {
                    Status = OptimizationSuccessResult.Failed,
                    Message = applyResult.Message,
                    FailedSteps = scope.GetStepResults().Where(step => !step.Success).ToList()
                };

            progress.Report(new ProcessingProgress
                { Message = Translations.Optimization_Apply_Completed, IsIndeterminate = false, Value = 1, Total = 1 });

            var result = scope.ToResult();
            await TrySaveRevertDataAsync(scope, optimization);

            return result;
        }
        catch (Exception ex)
        {
            var failedSteps = scope.GetStepResults().Where(step => !step.Success).ToList();

            var status = scope.HasSuccessfulSteps
                ? OptimizationSuccessResult.PartialSuccess
                : OptimizationSuccessResult.Failed;
            var result = new OptimizationResult
            {
                Status = status,
                Message = string.Format(Translations.Optimization_Apply_Error_Failed, optimization.Name),
                Exception = ex,
                FailedSteps = failedSteps
            };

            await TrySaveRevertDataAsync(scope, optimization);

            return result;
        }
    }

    public async Task<RevertResult> RevertAsync(IOptimization optimization,
        IProgress<ProcessingProgress>? progress = null)
    {
        progress?.Report(new ProcessingProgress
            { Message = Translations.Optimization_Revert_Reverting, IsIndeterminate = true });
        var result = await revertManager.RevertAsync(optimization, progress);
        progress?.Report(new ProcessingProgress
            { Message = result.Message, IsIndeterminate = false, Value = 1, Total = 1 });
        return result;
    }

    public static Task<bool> IsAppliedAsync(Guid id)
    {
        return RevertManager.IsAppliedAsync(id);
    }

    public static async Task UpdateOptimizationStateAsync(params IOptimization[] optimizations)
    {
        await Task.WhenAll(optimizations.Select(async opt =>
        {
            var data = await RevertManager.GetRevertDataAsync(opt.Id);
            opt.State.IsApplied = data != null;
            opt.State.AppliedAt = data?.AppliedAt;
        }));
    }

    public static Task UpdateOptimizationStateAsync(IEnumerable<IOptimization> optimizations)
    {
        return UpdateOptimizationStateAsync(optimizations.ToArray());
    }

    public static async Task<List<OperationStepResult>> RetryFailedStepsAsync(
        IReadOnlyList<OperationStepResult> failedSteps, bool reverseOrder, ILogger logger,
        IProgress<ProcessingProgress>? progress = null)
    {
        return (await RetryFailedStepsWithResultsAsync(failedSteps, reverseOrder, logger, progress)).FailedSteps;
    }

    public static async Task<RetryFailedStepsResult> RetryFailedStepsWithResultsAsync(
        IReadOnlyList<OperationStepResult> failedSteps, bool reverseOrder, ILogger logger,
        IProgress<ProcessingProgress>? progress = null)
    {
        if (failedSteps.Count == 0) return new RetryFailedStepsResult([], []);

        var remainingFailedSteps = new List<OperationStepResult>();
        var recoveredSteps = new List<OperationStepResult>();
        var orderedSteps =
            reverseOrder ? failedSteps.OrderByDescending(s => s.Index) : failedSteps.OrderBy(s => s.Index);
        var total = failedSteps.Count;
        var processedCount = 0;

        progress?.Report(new ProcessingProgress
            { Message = Translations.Optimization_Retry_Processing, IsIndeterminate = true });

        foreach (var step in orderedSteps)
        {
            processedCount++;
            progress?.Report(new ProcessingProgress
            {
                Message = string.Format(Translations.Optimization_RetryStep_Processing, step.Name, processedCount, total),
                IsIndeterminate = false,
                Value = processedCount,
                Total = total
            });

            if (step.RetryAction == null)
            {
                remainingFailedSteps.Add(step);
                continue;
            }

            var success = false;
            Exception? error = null;
            try
            {
                using var retryScope = ExecutionScope.BeginForCapture(logger);
                success = await step.RetryAction();

                if (success)
                {
                    var retriedStep = retryScope.SuccessfulSteps.LastOrDefault();
                    recoveredSteps.Add(retriedStep == null
                        ? step with { Error = null }
                        : new OperationStepResult
                        {
                            Index = step.Index,
                            Name = retriedStep.Name,
                            Description = retriedStep.Description,
                            Success = true,
                            Error = null,
                            RetryAction = null,
                            RevertStep = retriedStep.RevertStep
                        });
                }
            }
            catch (Exception ex)
            {
                error = ex;
                logger.LogError(ex, "Retry step {Name} failed", step.Name);
            }

            if (!success)
                remainingFailedSteps.Add(error != null ? step with { Error = error.Message } : step);
        }

        return new RetryFailedStepsResult(remainingFailedSteps, recoveredSteps);
    }

    private async Task TrySaveRevertDataAsync(ExecutionScope scope, IOptimization optimization)
    {
        if (!scope.HasSuccessfulSteps)
            return;

        try
        {
            await revertManager.SaveRevertDataAsync(scope);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save revert data for {Name}", optimization.OptimizationKey);
        }
    }

    public static void ClearResources(ILogger logger)
    {
        if (!Directory.Exists(Shared.ResourcesDirectory)) return;
        foreach (var f in Directory.GetFiles(Shared.ResourcesDirectory))
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

public enum RestorePointResult
{
    Success,
    Failed,
    FrequencyLimitReached
}

public sealed record RetryFailedStepsResult(
    List<OperationStepResult> FailedSteps,
    List<OperationStepResult> RecoveredSteps);
