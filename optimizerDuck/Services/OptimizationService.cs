using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Execution;
using optimizerDuck.Core.Models.Optimization;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Core.Models.Revert;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Managers;
using optimizerDuck.Services.OptimizationServices;
using optimizerDuck.UI.ViewModels.Dialogs;
using optimizerDuck.UI.Views.Dialogs;
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
            async Task<ShellResult> RunPowerShellAsync(string command)
            {
                return await Task.Run(() => ShellService.PowerShell(command));
            }

            dialogViewModel.ProgressReporter.Report(new ProcessingProgress
            {
                Message = Translations.RestorePoint_Progress_Creating,
                IsIndeterminate = true
            });

            using var scope = ExecutionScope.Begin(_logger);

            var result = await RunPowerShellAsync(
                $"Checkpoint-Computer -Description \"{Shared.RestorePointName}\" -RestorePointType MODIFY_SETTINGS");

            if (result.Stderr.Contains("already been created within the past 1440 minutes",
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
                _logger.LogError("Failed to create restore point (not a 'disabled' case): {Message}", result.Stderr);
                return RestorePointResult.Failed;
            }

            _logger.LogError("Failed to create restore point due to disabled feature: {Message}", result.Stderr);

            dialogViewModel.ProgressReporter.Report(new ProcessingProgress
            {
                Message = Translations.RestorePoint_Progress_Enabling,
                IsIndeterminate = true
            });

            _logger.LogInformation("Enabling System Protection on system drive...");
            var enableResult = await RunPowerShellAsync("Enable-ComputerRestore -Drive \"$env:SystemDrive\"");
            if (enableResult.ExitCode != 0)
            {
                _logger.LogError("Failed to enable System Protection on system drive: {Message}", enableResult.Stderr);
                return RestorePointResult.Failed;
            }

            dialogViewModel.ProgressReporter.Report(new ProcessingProgress
            {
                Message = Translations.RestorePoint_Progress_Retrying,
                IsIndeterminate = true
            });

            _logger.LogInformation("Retrying to create restore point...");
            result = await RunPowerShellAsync(
                $"Checkpoint-Computer -Description \"{Shared.RestorePointName}\" -RestorePointType MODIFY_SETTINGS");

            if (result.Stderr.Contains("already been created within the past 1440 minutes",
                    StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Restore point creation skipped: frequency limit reached.");
                return RestorePointResult.FrequencyLimitReached;
            }

            if (result.ExitCode == 0)
                return RestorePointResult.Success;

            if (result.Stderr.Contains("is disabled", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Failed to create restore point after enabling feature: {Message}", result.Stderr);
                return RestorePointResult.Failed;
            }

            _logger.LogError("Failed to create restore point: {Message}", result.Stderr);
            return RestorePointResult.Failed;
        }
        finally
        {
            dialog.Hide();
        }
    }

    #region Main Methods

    /// <summary>
    ///     Apply an optimization.
    /// </summary>
    /// <param name="optimization">The optimization to apply.</param>
    /// <param name="progress">The progress reporter.</param>
    /// <returns>The result of the optimization.</returns>
    public async Task<OptimizationResult> ApplyAsync(IOptimization optimization,
        IProgress<ProcessingProgress> progress)
    {
        var optimizationLogger = loggerFactory.CreateLogger(optimization.GetType());
        using var executionScope = ExecutionScope.Begin(optimization, optimizationLogger);

        OptimizationResult result;

        try
        {
            progress.Report(new ProcessingProgress
            {
                Message = Translations.Optimization_Apply_Processing,
                IsIndeterminate = true
            });

            var applyResult = await Task.Run(async () =>
                await optimization.ApplyAsync(progress,
                    new OptimizationContext
                    {
                        Logger = optimizationLogger,
                        Snapshot = systemInfoService.Snapshot,
                        StreamService = streamService
                    }));

            if (!string.IsNullOrWhiteSpace(applyResult.Message))
            {
                var failedSteps = executionScope.FailedSteps;

                return new OptimizationResult
                {
                    Status = OptimizationSuccessResult.Failed,
                    Message = applyResult.Message,
                    FailedSteps = failedSteps.Select(step => new OperationStepResult
                    {
                        Index = step.Index,
                        Name = step.Name,
                        Description = step.Description,
                        Success = step.Success,
                        Error = step.Error,
                        RetryAction = step.RetryAction
                    }).ToList()
                };
            }

            progress.Report(new ProcessingProgress
            {
                Message = Translations.Optimization_Apply_Completed,
                IsIndeterminate = false,
                Value = 1,
                Total = 1
            });

            result = executionScope.ToResult();

            if (executionScope.HasSuccessfulSteps)
                try
                {
                    await revertManager.SaveRevertDataAsync(executionScope);
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "Failed to save revert data for {Name}, but optimization was applied",
                        optimization.OptimizationKey);
                }
        }
        catch (Exception ex)
        {
            var failedStepResults = executionScope.FailedSteps.Select(step => new OperationStepResult
            {
                Index = step.Index,
                Name = step.Name,
                Description = step.Description,
                Success = step.Success,
                Error = step.Error,
                RetryAction = step.RetryAction
            }).ToList();

            var hasAnySuccess = executionScope.HasSuccessfulSteps;
            var status = hasAnySuccess
                ? OptimizationSuccessResult.PartialSuccess
                : OptimizationSuccessResult.Failed;
            result = new OptimizationResult
            {
                Status = status,
                Message = string.Format(Translations.Optimization_Apply_Error_Failed,
                    optimization.Name),
                Exception = ex,
                FailedSteps = failedStepResults
            };

            if (executionScope.HasSuccessfulSteps)
                try
                {
                    await revertManager.SaveRevertDataAsync(executionScope);
                    _logger.LogWarning("Saved revert data for {Name} despite exception", optimization.OptimizationKey);
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "Critical: Failed to save revert data after exception for {Name}",
                        optimization.OptimizationKey);
                }
        }

        return result;
    }

    /// <summary>
    ///     Revert an optimization.
    /// </summary>
    /// <param name="optimization">The optimization to revert.</param>
    /// <param name="progress">The progress reporter.</param>
    /// <returns>The result of the revert.</returns>
    public async Task<RevertResult> RevertAsync(IOptimization optimization,
        IProgress<ProcessingProgress>? progress = null)
    {
        var optimizationLogger = loggerFactory.CreateLogger(optimization.GetType());

        progress?.Report(new ProcessingProgress
        {
            Message = Translations.Optimization_Revert_Reverting,
            IsIndeterminate = true
        });

        var result = await revertManager.RevertAsync(
            optimization,
            progress);

        progress?.Report(new ProcessingProgress
        {
            Message = result.Message,
            IsIndeterminate = false,
            Value = 1,
            Total = 1
        });

        return result;
    }

    #endregion Main Methods

    #region Helpers

    /// <summary>
    ///     Check if an optimization is applied.
    /// </summary>
    /// <param name="optimizationId">The ID of the optimization.</param>
    /// <returns>Whether the optimization is applied.</returns>
    public static Task<bool> IsAppliedAsync(Guid optimizationId)
    {
        return RevertManager.IsAppliedAsync(optimizationId);
    }

    /// <summary>
    ///     Update the state of optimizations.
    /// </summary>
    /// <param name="optimizations">The optimizations to update.</param>
    public static async Task UpdateOptimizationStateAsync(params IOptimization[] optimizations)
    {
        var tasks = optimizations.Select(async optimization =>
        {
            var revertData = await RevertManager.GetRevertDataAsync(optimization.Id);
            optimization.State.IsApplied = revertData != null;
            optimization.State.AppliedAt = revertData?.AppliedAt;
        });

        await Task.WhenAll(tasks);
    }

    public static async Task UpdateOptimizationStateAsync(IEnumerable<IOptimization> optimizations)
    {
        await UpdateOptimizationStateAsync(optimizations.ToArray());
    }

    /// <summary>
    ///     Retry failed steps for an optimization.
    /// </summary>
    /// <param name="failedSteps">The failed steps.</param>
    /// <param name="reverseOrder">Whether to reverse the order of the steps.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="progress">The progress reporter.</param>
    /// <returns>The result of the retry.</returns>
    public static async Task<List<OperationStepResult>> RetryFailedStepsAsync(
        IReadOnlyList<OperationStepResult> failedSteps,
        bool reverseOrder,
        ILogger logger,
        IProgress<ProcessingProgress>? progress = null)
    {
        if (failedSteps.Count == 0)
            return [];

        var stillFailed = new List<OperationStepResult>();

        // Order steps by index, reverse if reverseOrder is true (revert flow)
        var orderedSteps = reverseOrder
            ? failedSteps.OrderByDescending(s => s.Index)
            : failedSteps.OrderBy(s => s.Index);

        var totalSteps = failedSteps.Count;

        progress?.Report(new ProcessingProgress
        {
            Message = Translations.Optimization_Retry_Processing,
            IsIndeterminate = true,
            Value = 0,
            Total = 0
        });

        foreach (var step in orderedSteps)
        {
            progress?.Report(new ProcessingProgress
            {
                Message = string.Format(Translations.Optimization_RetryStep_Processing, step.Name, step.Index,
                    totalSteps),
                IsIndeterminate = false,
                Value = step.Index,
                Total = totalSteps
            });

            if (step.RetryAction == null)
            {
                stillFailed.Add(step);
                continue;
            }

            var success = false;
            Exception? lastEx = null;

            try
            {
                success = await step.RetryAction();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Retry step {Name} failed", step.Name);
                lastEx = ex;
            }

            if (!success)
                stillFailed.Add(lastEx != null ? step with { Error = lastEx.Message } : step);
        }

        return stillFailed;
    }

    /// <summary>
    ///     Clear resources.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public static void ClearResources(ILogger logger)
    {
        if (!Directory.Exists(Shared.ResourcesDirectory))
            return;

        foreach (var filePath in Directory.GetFiles(Shared.ResourcesDirectory))
            try
            {
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete resource file {File}", filePath);
            }
    }

    #endregion Helpers
}

/// <summary>
///     The result of a restore point operation.
/// </summary>
public enum RestorePointResult
{
    /// <summary>
    ///     The restore point was created successfully.
    /// </summary>
    Success,
    /// <summary>
    ///     The restore point creation failed.
    /// </summary>
    Failed,
    /// <summary>
    ///     The restore point creation was skipped due to a frequency limit.
    /// </summary>
    FrequencyLimitReached
}