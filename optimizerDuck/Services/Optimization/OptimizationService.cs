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
using optimizerDuck.Services.Optimization.Providers;
using optimizerDuck.UI.Dialogs;
using optimizerDuck.UI.ViewModels.Dialogs;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.Services;

public class OptimizationService(
    RevertManager revertManager,
    ILoggerFactory loggerFactory,
    SystemInfoService systemInfoService,
    StreamService streamService,
    IContentDialogService contentDialogService,
    ILogger<OptimizationService> logger
)
{
    private readonly ILogger _logger = logger;

    /// <summary>Gets or sets a value that indicates whether a system restore point was created before applying optimizations.</summary>
    public bool WasRequestedRestorePoint { get; set; } = false;

    private static readonly Regex _restorePointDisabledRegex = new(
        @"\b(is\s+disabled|system\s+restore\s+is\s+disabled|disabled\s+by\s+group\s+policy|disableconfig|disablesr|protection\s+is\s+off)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled
    );

    /// <summary>Creates a system restore point via PowerShell, showing a processing dialog. Enables System Protection if it is disabled.</summary>
    /// <returns>A <see cref="RestorePointResult"/> indicating success, failure, or frequency-limit reached.</returns>
    public async Task<RestorePointResult> CreateRestorePointAsync()
    {
        var dialogViewModel = new ProcessingViewModel();
        var dialog = new ContentDialog
        {
            Title = Translations.RestorePoint_Title,
            Content = new ProcessingDialog { DataContext = dialogViewModel },
            IsFooterVisible = false,
        };

        _ = contentDialogService.ShowAsync(dialog, CancellationToken.None);

        try
        {
            dialogViewModel.ProgressReporter.Report(
                new ProcessingProgress
                {
                    Message = Translations.RestorePoint_Progress_Creating,
                    IsIndeterminate = true,
                }
            );

            using var scope = ExecutionScope.BeginForLogging(_logger);

            var result = await ShellService.PowerShellAsync(
                $"Checkpoint-Computer -Description \"{Shared.RestorePointName}\" -RestorePointType MODIFY_SETTINGS"
            );

            if (IsFrequencyLimited(result.Stderr))
            {
                _logger.LogWarning("Restore point creation skipped: frequency limit reached.");
                return RestorePointResult.FrequencyLimitReached;
            }

            if (result.ExitCode == 0)
            {
                _logger.LogInformation("Restore point created successfully.");
                return RestorePointResult.Success;
            }

            if (!_restorePointDisabledRegex.IsMatch(result.Stderr))
            {
                _logger.LogError("Failed to create restore point: {Message}", result.Stderr);
                return RestorePointResult.Failed;
            }

            _logger.LogInformation("System Protection is disabled. Enabling...");
            dialogViewModel.ProgressReporter.Report(
                new ProcessingProgress
                {
                    Message = Translations.RestorePoint_Progress_Enabling,
                    IsIndeterminate = true,
                }
            );

            var enableResult = await ShellService.PowerShellAsync(
                "Enable-ComputerRestore -Drive \"$env:SystemDrive\""
            );

            if (enableResult.ExitCode != 0)
            {
                _logger.LogError(
                    "Failed to enable System Protection: {Message}",
                    enableResult.Stderr
                );
                return RestorePointResult.Failed;
            }

            dialogViewModel.ProgressReporter.Report(
                new ProcessingProgress
                {
                    Message = Translations.RestorePoint_Progress_Retrying,
                    IsIndeterminate = true,
                }
            );

            result = await ShellService.PowerShellAsync(
                $"Checkpoint-Computer -Description \"{Shared.RestorePointName}\" -RestorePointType MODIFY_SETTINGS"
            );

            if (IsFrequencyLimited(result.Stderr))
                return RestorePointResult.FrequencyLimitReached;

            return result.ExitCode == 0 ? RestorePointResult.Success : RestorePointResult.Failed;
        }
        finally
        {
            dialog?.Hide();
        }
    }

    private static bool IsFrequencyLimited(string? stderr)
    {
        return stderr?.Contains(
                "already been created within the past",
                StringComparison.OrdinalIgnoreCase
            ) == true;
    }

    /// <summary>Applies the specified optimization, captures revert steps into an execution scope, and persists revert data on any successful steps.</summary>
    /// <param name="optimization">The optimization to apply.</param>
    /// <param name="progress">An <see cref="IProgress{T}"/> to report application progress.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An <see cref="OptimizationResult"/> describing the outcome, including partial-success or failure details.</returns>
    public async Task<OptimizationResult> ApplyAsync(
        IOptimization optimization,
        IProgress<ProcessingProgress> progress,
        CancellationToken cancellationToken = default
    )
    {
        var optLogger = loggerFactory.CreateLogger(optimization.GetType());
        using var scope = ExecutionScope.Begin(optimization, optLogger);

        progress.Report(
            new ProcessingProgress
            {
                Message = Translations.Optimization_Apply_Processing,
                IsIndeterminate = true,
            }
        );

        try
        {
            var applyResult = await optimization.ApplyAsync(
                progress,
                new OptimizationContext
                {
                    Logger = optLogger,
                    Snapshot = systemInfoService.Snapshot,
                    StreamService = streamService,
                }
            );

            if (!string.IsNullOrWhiteSpace(applyResult.ErrorMessage))
            {
                if (scope.HasSuccessfulSteps)
                    await TrySaveRevertDataAsync(scope, optimization);

                return new OptimizationResult
                {
                    Status = scope.HasSuccessfulSteps
                        ? OptimizationSuccessResult.PartialSuccess
                        : OptimizationSuccessResult.Failed,
                    Message = applyResult.ErrorMessage,
                    FailedSteps = [.. scope.GetStepResults().Where(step => !step.Success)],
                };
            }

            progress.Report(
                new ProcessingProgress
                {
                    Message = Translations.Optimization_Apply_Completed,
                    IsIndeterminate = false,
                    Value = 1,
                    Total = 1,
                }
            );

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
                Message = string.Format(
                    Translations.Optimization_Apply_Error_Failed,
                    optimization.Name
                ),
                Exception = ex,
                FailedSteps = failedSteps,
            };

            await TrySaveRevertDataAsync(scope, optimization);

            return result;
        }
    }

    /// <summary>Reverts the specified optimization using stored revert data from a previous apply operation.</summary>
    /// <param name="optimization">The optimization to revert.</param>
    /// <param name="progress">An optional <see cref="IProgress{T}"/> to report revert progress.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="RevertResult"/> describing the outcome.</returns>
    public async Task<RevertResult> RevertAsync(
        IOptimization optimization,
        IProgress<ProcessingProgress>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        progress?.Report(
            new ProcessingProgress
            {
                Message = Translations.Optimization_Revert_Reverting,
                IsIndeterminate = true,
            }
        );
        var result = await revertManager.RevertAsync(optimization, progress);
        progress?.Report(
            new ProcessingProgress
            {
                Message = result.Message,
                IsIndeterminate = false,
                Value = 1,
                Total = 1,
            }
        );
        return result;
    }

    /// <summary>Updates the applied state of the specified optimizations by scanning revert data files on disk. An optimization is considered applied when its revert JSON file exists.</summary>
    /// <param name="optimizations">The optimizations whose state to update.</param>
    public static async Task UpdateOptimizationStateAsync(params IOptimization[] optimizations)
    {
        if (optimizations.Length == 0)
            return;

        // scan revert directory for which optimizations are currently applied
        // we infer applied state from file presence, not a database
        var revertFiles = await Task.Run(() =>
        {
            if (!Directory.Exists(Shared.RevertDirectory))
                return new HashSet<string>();

            return Directory
                .GetFiles(Shared.RevertDirectory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(f => f != null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
        });

        foreach (var opt in optimizations)
        {
            var idStr = opt.Id.ToString();
            if (revertFiles.Contains(idStr))
            {
                var data = await RevertManager.GetRevertDataAsync(opt.Id);
                opt.State.IsApplied = data != null;
                opt.State.AppliedAt = data?.AppliedAt;
            }
            else
            {
                opt.State.IsApplied = false;
                opt.State.AppliedAt = null;
            }
        }
    }

    /// <summary>Updates the applied state of the specified optimizations by scanning revert data files on disk.</summary>
    /// <param name="optimizations">The optimizations whose state to update.</param>
    public static Task UpdateOptimizationStateAsync(IEnumerable<IOptimization> optimizations)
    {
        return UpdateOptimizationStateAsync(optimizations.ToArray());
    }

    /// <summary>Retries the specified failed operation steps, optionally in reverse order. Automatically persists recovered revert steps if a <see cref="RevertManager"/> is provided.</summary>
    /// <param name="failedSteps">The steps that failed and should be retried.</param>
    /// <param name="reverseOrder">If <see langword="true"/>, retries steps in descending index order (useful for revert operations).</param>
    /// <param name="logger">The logger for retry diagnostics.</param>
    /// <param name="revertManager">Optional revert manager to persist recovered revert steps.</param>
    /// <param name="optimizationId">The optimization ID for revert step persistence.</param>
    /// <param name="optimizationKey">The optimization key for revert step persistence.</param>
    /// <param name="progress">An optional progress reporter.</param>
    /// <returns>The list of steps that remain failed after retry.</returns>
    public static async Task<List<OperationStepResult>> RetryFailedStepsAsync(
        IReadOnlyList<OperationStepResult> failedSteps,
        bool reverseOrder,
        ILogger logger,
        RevertManager? revertManager = null,
        Guid? optimizationId = null,
        string? optimizationKey = null,
        IProgress<ProcessingProgress>? progress = null
    )
    {
        return (
            await RetryFailedStepsWithResultsAsync(
                failedSteps,
                reverseOrder,
                logger,
                revertManager,
                optimizationId,
                optimizationKey,
                progress
            )
        ).FailedSteps;
    }

    /// <summary>Retries failed operation steps and returns both recovered and remaining failed steps for detailed inspection. Automatically persists recovered revert steps if a <see cref="RevertManager"/> is provided.</summary>
    /// <param name="failedSteps">The steps that failed and should be retried.</param>
    /// <param name="reverseOrder">If <see langword="true"/>, retries steps in descending index order.</param>
    /// <param name="logger">The logger for retry diagnostics.</param>
    /// <param name="revertManager">Optional revert manager to persist recovered revert steps.</param>
    /// <param name="optimizationId">The optimization ID for revert step persistence.</param>
    /// <param name="optimizationKey">The optimization key for revert step persistence.</param>
    /// <param name="progress">An optional progress reporter.</param>
    /// <returns>A <see cref="RetryFailedStepsResult"/> containing both recovered and remaining failed steps.</returns>
    public static async Task<RetryFailedStepsResult> RetryFailedStepsWithResultsAsync(
        IReadOnlyList<OperationStepResult> failedSteps,
        bool reverseOrder,
        ILogger logger,
        RevertManager? revertManager = null,
        Guid? optimizationId = null,
        string? optimizationKey = null,
        IProgress<ProcessingProgress>? progress = null
    )
    {
        if (failedSteps.Count == 0)
            return new RetryFailedStepsResult([], []);

        var remainingFailedSteps = new List<OperationStepResult>();
        var recoveredSteps = new List<OperationStepResult>();
        var orderedSteps = reverseOrder
            ? failedSteps.OrderByDescending(s => s.Index)
            : failedSteps.OrderBy(s => s.Index);
        var total = failedSteps.Count;
        var processedCount = 0;

        progress?.Report(
            new ProcessingProgress
            {
                Message = Translations.Optimization_Retry_Processing,
                IsIndeterminate = true,
            }
        );

        foreach (var step in orderedSteps)
        {
            processedCount++;
            progress?.Report(
                new ProcessingProgress
                {
                    Message = string.Format(
                        Translations.Optimization_RetryStep_Processing,
                        step.Name,
                        processedCount,
                        total
                    ),
                    Value = processedCount,
                    Total = total,
                }
            );

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
                    var recoveredStep =
                        retriedStep == null
                            ? step with
                            {
                                Error = null,
                            }
                            : new OperationStepResult
                            {
                                Index = step.Index,
                                Name = retriedStep.Name,
                                Description = retriedStep.Description,
                                Success = true,
                                Error = null,
                                RetryAction = null,
                                RevertStep = retriedStep.RevertStep,
                            };

                    // Auto-persist recovered revert step if revertManager is available
                    if (
                        recoveredStep.RevertStep != null
                        && revertManager != null
                        && optimizationId.HasValue
                    )
                    {
                        try
                        {
                            await revertManager.UpsertRevertStepAtIndexAsync(
                                optimizationId.Value,
                                optimizationKey ?? string.Empty,
                                step.Index, // Use original failed step's index
                                recoveredStep.RevertStep
                            );
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(
                                ex,
                                "Failed to auto-persist revert step at index {Index}",
                                step.Index
                            );
                        }
                    }

                    recoveredSteps.Add(recoveredStep);
                }
            }
            catch (Exception ex)
            {
                error = ex;
                logger.LogError(ex, "Retry step {Name} failed", step.Name);
            }

            if (!success)
                remainingFailedSteps.Add(
                    error != null ? step with { Error = error.Message } : step
                );
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
            _logger.LogError(
                ex,
                "Failed to save revert data for {Name}",
                optimization.OptimizationKey
            );
        }
    }

    /// <summary>Deletes all files in the downloads directory. Silently skips files that cannot be deleted.</summary>
    /// <param name="logger">The logger for deletion errors.</param>
    public static void ClearDownloads(ILogger logger)
    {
        if (!Directory.Exists(Shared.DownloadsDirectory))
            return;
        foreach (var f in Directory.GetFiles(Shared.DownloadsDirectory))
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

/// <summary>Represents the result of a system restore point creation attempt.</summary>
public enum RestorePointResult
{
    /// <summary>The restore point was created successfully.</summary>
    Success,

    /// <summary>The restore point creation failed.</summary>
    Failed,

    /// <summary>The creation was skipped because a restore point was already created within the frequency limit (24 hours).</summary>
    FrequencyLimitReached,
}

/// <summary>Represents the result of retrying failed operation steps, separating recovered steps from those that remain failed.</summary>
/// <param name="FailedSteps">The steps that remain failed after retry.</param>
/// <param name="RecoveredSteps">The steps that succeeded on retry.</param>
public sealed record RetryFailedStepsResult(
    List<OperationStepResult> FailedSteps,
    List<OperationStepResult> RecoveredSteps
);
