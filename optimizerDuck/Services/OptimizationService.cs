using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Interfaces;
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

/// <summary>
///     Provides services for applying and reverting optimizations.
/// </summary>
public class OptimizationService(
    RevertManager revertManager,
    ILoggerFactory loggerFactory,
    SystemInfoService systemInfoService,
    StreamService streamService,
    IContentDialogService contentDialogService,
    ILogger<OptimizationService> logger)
{
    private static readonly Dictionary<Guid, (bool IsApplied, DateTime? AppliedAt)> _stateCache = new();
    private static readonly object _cacheLock = new();
    private readonly ILogger _logger = logger;

    /// <summary>
    ///     Indicates whether a system restore point was requested before applying optimizations.
    /// </summary>
    public bool WasRequestedRestorePoint { get; set; } = false;

    /// <summary>
    ///     Creates a Windows system restore point.
    /// </summary>
    /// <returns>
    ///     A task that completes with a <see cref="RestorePointResult" /> indicating success,
    ///     failure, or if the frequency limit was reached.
    /// </returns>
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

            using var tracker = ServiceTracker.Begin(_logger);

            var result = await RunPowerShellAsync(
                $"Checkpoint-Computer -Description \"{Shared.RestorePointName}\" -RestorePointType MODIFY_SETTINGS");

            // it gives stderr but exitcode is 0
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

            // try to enable it
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

            // retry creating restore point
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
    ///     Applies an optimization to the system.
    /// </summary>
    /// <param name="optimization">The optimization to apply.</param>
    /// <param name="progress">A progress reporter for UI updates.</param>
    /// <returns>A task that completes with the optimization result.</returns>
    public async Task<OptimizationResult> ApplyAsync(IOptimization optimization,
        IProgress<ProcessingProgress> progress)
    {
        var optimizationLogger = loggerFactory.CreateLogger(optimization.GetType());

        OptimizationResult result;
        List<OperationStepResult> stepResults = [];
        var tracker = ServiceTracker.Begin(optimizationLogger);

        RevertContext? revertContext = null;
        var revertContextDisposed = false;

        try
        {
            revertContext = revertManager.BeginRecording(optimization);

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

            if (!string.IsNullOrWhiteSpace(applyResult.Message)) // Optimization failed
            {
                // Retry to recover from failed steps
                stepResults = tracker.GetSteps().ToList();
                var failedApplySteps = stepResults.Where(s => !s.Success).ToList();

                // Persist revert steps first, then attempt an immediate revert.
                await revertContext.DisposeAsync();
                revertContextDisposed = true;

                var revertResult = await revertManager.RevertAsync(
                    optimization.Id,
                    optimization.OptimizationKey,
                    progress);

                // If revert succeeds, remove any cached applied state.
                if (revertResult.Success)
                    lock (_cacheLock)
                    {
                        _stateCache.Remove(optimization.Id);
                    }

                return new OptimizationResult
                {
                    Status = OptimizationSuccessResult.Failed,
                    Message = applyResult.Message,
                    FailedSteps = failedApplySteps
                };
            }

            progress.Report(new ProcessingProgress
            {
                Message = Translations.Optimization_Apply_Completed,
                IsIndeterminate = false,
                Value = 1,
                Total = 1
            });

            // Collect steps after apply completes.
            stepResults = tracker.GetSteps().ToList();
            var failedSteps = stepResults.Where(s => !s.Success).ToList();

            var status = ResolveStatus(stepResults, failedSteps);
            result = new OptimizationResult
            {
                Status = status,
                Message = status == OptimizationSuccessResult.Success
                    ? string.Format(Translations.Optimization_Apply_Success, optimization.OptimizationKey)
                    : stepResults.Count == failedSteps.Count // if all steps failed
                        ? string.Format(Translations.Optimization_Apply_Error_Failed, optimization.OptimizationKey)
                        : string.Format(Translations.Optimization_Apply_Error_FailedWithSteps,
                            optimization.OptimizationKey,
                            failedSteps.Count),
                FailedSteps = failedSteps
            };

            // Update cache after successful apply
            if (status == OptimizationSuccessResult.Success)
                lock (_cacheLock)
                {
                    _stateCache[optimization.Id] = (true, DateTime.Now);
                }

            if (!revertContextDisposed)
                await revertContext.DisposeAsync();
        }
        catch (Exception ex)
        {
            stepResults = tracker?.GetSteps().ToList() ?? [];
            var failedSteps = stepResults.Where(s => !s.Success).ToList();

            var status = ResolveStatus(stepResults, failedSteps);
            result = new OptimizationResult
            {
                Status = status,
                Message = string.Format(Translations.Optimization_Apply_Error_Failed,
                    optimization.OptimizationKey),
                Exception = ex,
                FailedSteps = failedSteps
            };

            // Best-effort: persist any revert steps that were recorded before the exception.
            if (revertContext is not null && !revertContextDisposed)
                try
                {
                    await revertContext.DisposeAsync();
                    revertContextDisposed = true;
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "Failed to save revert data after exception for {Name}",
                        optimization.OptimizationKey);
                }
        }
        finally
        {
            tracker?.Dispose();
        }

        return result;

        OptimizationSuccessResult ResolveStatus(
            IReadOnlyCollection<OperationStepResult> steps,
            IReadOnlyCollection<OperationStepResult> failedSteps)
        {
            if (steps.Count == 0)
                return OptimizationSuccessResult.Failed;

            if (failedSteps.Count == 0)
                return OptimizationSuccessResult.Success;

            return failedSteps.Count < steps.Count
                ? OptimizationSuccessResult.PartialSuccess
                : OptimizationSuccessResult.Failed;
        }
    }

    /// <summary>
    ///     Reverts a previously applied optimization.
    /// </summary>
    /// <param name="optimization">The optimization to revert.</param>
    /// <param name="progress">A progress reporter for UI updates (optional).</param>
    /// <returns>A task that completes with the revert result.</returns>
    public async Task<RevertResult> RevertAsync(IOptimization optimization,
        IProgress<ProcessingProgress>? progress = null)
    {
        var optimizationLogger = loggerFactory.CreateLogger(optimization.GetType());
        using var tracker = ServiceTracker.Begin(optimizationLogger);

        progress?.Report(new ProcessingProgress
        {
            Message = Translations.Optimization_Revert_Reverting,
            IsIndeterminate = true
        });

        var result = await revertManager.RevertAsync(
            optimization.Id,
            optimization.OptimizationKey,
            progress);

        // Merge tracker steps with revert result details
        var trackerSteps = tracker.GetSteps().ToList();
        if (result.FailedStepDetails.Count == 0 && trackerSteps.Any(s => !s.Success))
            result.FailedStepDetails = trackerSteps.Where(s => !s.Success).ToList();

        progress?.Report(new ProcessingProgress
        {
            Message = result.Message,
            IsIndeterminate = false,
            Value = 1,
            Total = 1
        });

        // Update cache after revert
        if (result.Success)
            lock (_cacheLock)
            {
                _stateCache.Remove(optimization.Id);
            }

        return result;
    }

    #endregion Main Methods

    #region Helpers

    /// <summary>
    ///     Checks whether an optimization has been applied.
    /// </summary>
    /// <param name="optimizationId">The unique identifier of the optimization.</param>
    /// <returns>True if the optimization was applied; otherwise, false.</returns>
    public static Task<bool> IsAppliedAsync(Guid optimizationId)
    {
        return RevertManager.IsAppliedAsync(optimizationId);
    }

    /// <summary>
    ///     Updates the applied state for a collection of optimizations.
    /// </summary>
    /// <param name="optimizations">The optimizations to update.</param>
    public static async Task UpdateOptimizationStateAsync(params IOptimization[] optimizations)
    {
        var optimizationIds = optimizations.Select(o => o.Id).ToArray();
        var uncachedIds = new List<Guid>();

        lock (_cacheLock)
        {
            foreach (var optimization in optimizations)
                if (_stateCache.TryGetValue(optimization.Id, out var cachedState))
                {
                    // Use cached state
                    optimization.State.IsApplied = cachedState.IsApplied;
                    optimization.State.AppliedAt = cachedState.AppliedAt;
                }
                else
                {
                    uncachedIds.Add(optimization.Id);
                }
        }

        // Only fetch from disk for uncached optimizations
        if (uncachedIds.Count > 0)
        {
            var tasks = uncachedIds.Select(async id =>
            {
                var revertData = await RevertManager.GetRevertDataAsync(id);
                var state = (IsApplied: revertData != null, revertData?.AppliedAt);

                lock (_cacheLock)
                {
                    _stateCache[id] = state;
                }

                return (Id: id, State: state);
            });

            var results = await Task.WhenAll(tasks);

            // Update optimizations with fetched data
            foreach (var optimization in optimizations)
            {
                var result = results.FirstOrDefault(r => r.Id == optimization.Id);
                if (result.Id != Guid.Empty)
                {
                    optimization.State.IsApplied = result.State.IsApplied;
                    optimization.State.AppliedAt = result.State.AppliedAt;
                }
            }
        }
    }

    /// <summary>
    ///     Updates the applied state for a collection of optimizations.
    /// </summary>
    /// <param name="optimizations">The optimizations to update.</param>
    public static async Task UpdateOptimizationStateAsync(IEnumerable<IOptimization> optimizations)
    {
        await UpdateOptimizationStateAsync(optimizations.ToArray());
    }

    /// <summary>
    ///     Retries failed optimization steps with configurable max retry attempts.
    /// </summary>
    /// <param name="failedSteps">The steps that previously failed.</param>
    /// <param name="reverseOrder">Whether to execute steps in reverse order.</param>
    /// <param name="progress">A progress reporter for UI updates (optional).</param>
    /// <param name="maxRetries">Maximum number of retry attempts per step (default 3).</param>
    /// <returns>A list of steps that still failed after retry.</returns>
    public static async Task<List<OperationStepResult>> RetryFailedStepsAsync(
        IReadOnlyList<OperationStepResult> failedSteps,
        bool reverseOrder,
        IProgress<ProcessingProgress>? progress = null,
        int maxRetries = 3)
    {
        if (failedSteps.Count == 0)
            return [];

        var stillFailed = new List<OperationStepResult>();

        var orderedSteps = reverseOrder
            ? failedSteps.OrderByDescending(s => s.Index)
            : failedSteps.OrderBy(s => s.Index);
        var totalSteps = failedSteps.Count;
        var stepIndex = 0;

        progress?.Report(new ProcessingProgress
        {
            Message = Translations.Optimization_Retry_Processing,
            IsIndeterminate = true,
            Value = 0,
            Total = 0
        });

        foreach (var step in orderedSteps)
        {
            stepIndex++;

            progress?.Report(new ProcessingProgress
            {
                Message = string.Format(Translations.Optimization_RetryStep_Processing, step.Name, stepIndex,
                    totalSteps),
                IsIndeterminate = false,
                Value = stepIndex,
                Total = totalSteps
            });

            if (step.RetryAction == null)
            {
                stillFailed.Add(step);
                continue;
            }

            var success = false;
            for (var attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    success = await step.RetryAction();
                    if (success) break;
                }
                catch
                {
                    // Retry on next attempt
                }

                if (attempt < maxRetries - 1)
                    await Task.Delay(200 * (attempt + 1)); // progressive delay
            }

            if (!success)
                stillFailed.Add(step);
        }

        return stillFailed;
    }

    public static void ClearResourcesAsync()
    {
        lock (_cacheLock)
        {
            _stateCache.Clear();
        }

        if (!Directory.Exists(Shared.ResourcesDirectory))
            return;

        foreach (var filePath in Directory.GetFiles(Shared.ResourcesDirectory))
            try
            {
                File.Delete(filePath);
            }
            catch
            {
                // Best-effort cleanup only.
            }
    }

    public static void ClearStateCache()
    {
        lock (_cacheLock)
        {
            _stateCache.Clear();
        }
    }

    #endregion Helpers
}

public enum RestorePointResult
{
    Success,
    Failed,
    FrequencyLimitReached
}