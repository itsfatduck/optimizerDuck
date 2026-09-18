using System.IO;
using System.Management;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.Revert;
using optimizerDuck.Domain.UI;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.History;
using optimizerDuck.Services.Revert;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.UI.Dialogs;
using optimizerDuck.UI.ViewModels.Dialogs;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.Services.Optimization;

/// <summary>
///     Applies and reverts optimizations, and owns the revert, retry and applied-state paths.
/// </summary>
public class OptimizationService(
    OperationRunner runner,
    RevertManager revertManager,
    ILoggerFactory loggerFactory,
    IContentDialogService contentDialogService,
    SystemRestoreService systemRestoreService,
    ILogger<OptimizationService> logger
)
{
    private readonly ILogger _logger = logger;
    private readonly SystemRestoreService _systemRestoreService = systemRestoreService;

    /// <summary>
    ///     Gets or sets a value that indicates whether a system restore point was created before
    ///     applying optimizations.
    /// </summary>
    public bool WasRequestedRestorePoint { get; set; } = false;

    /// <summary>
    ///     Creates a restore point through the System Restore subsystem, showing a processing
    ///     dialog. Enables System Protection for the system drive and retries once when Windows
    ///     reports protection as disabled.
    ///     <para>
    ///     This is a UI orchestration method: it must be awaited from the UI thread and it keeps
    ///     that thread for itself, because it shows, updates and hides a
    ///     <see cref="ContentDialog" />. Only the native calls are pushed onto the thread pool, so
    ///     a slow service call never
    ///     blocks the interface.
    ///     </para>
    /// </summary>
    /// <returns>
    ///     A <see cref="RestorePointResult"/> indicating success, failure, or frequency-limit
    ///     reached.
    /// </returns>
    public async Task<RestorePointResult> CreateRestorePointAsync()
    {
        var dialogViewModel = new ProcessingViewModel();
        var dialog = new ContentDialog
        {
            Title = Loc.Instance["RestorePoint.Title"],
            Content = new ProcessingDialog { DataContext = dialogViewModel },
            IsFooterVisible = false,
        };

        _ = contentDialogService.ShowAsync(dialog, CancellationToken.None);

        try
        {
            // Throttle is the documented frequency setting plus the newest existing point; the
            // service fails open when that state cannot be read, exactly like the create below.
            var nowUtc = DateTime.UtcNow;
            if (await Task.Run(() => _systemRestoreService.IsWithinCreationThrottle(nowUtc)))
            {
                _logger.LogWarning("Restore point creation skipped: frequency limit reached.");
                return RestorePointResult.FrequencyLimitReached;
            }

            dialogViewModel.ProgressReporter.Report(
                new ProcessingProgress
                {
                    Message = Loc.Instance["RestorePoint.Progress.Creating"],
                    IsIndeterminate = true,
                }
            );

            var first = await Task.Run(() =>
                _systemRestoreService.CreateRestorePoint(Shared.RestorePointName)
            );

            if (first.ExceptionText is not null)
                _logger.LogError("Restore point creation failed: {Message}", first.ExceptionText);

            if (first.Succeeded)
            {
                _logger.LogInformation("Restore point created successfully.");
                return RestorePointResult.Success;
            }

            if (!SystemRestoreService.IsProtectionDisabledStatus(first.NativeStatus))
            {
                _logger.LogError(
                    "Failed to create restore point: native status 0x{Status:X8}",
                    first.NativeStatus
                );
                return RestorePointResult.Failed;
            }

            _logger.LogInformation("System Protection is disabled. Enabling...");
            dialogViewModel.ProgressReporter.Report(
                new ProcessingProgress
                {
                    Message = Loc.Instance["RestorePoint.Progress.Enabling"],
                    IsIndeterminate = true,
                }
            );

            var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            var enable = await Task.Run(() => _systemRestoreService.EnableProtection(systemDrive));
            if (!enable.Succeeded)
            {
                _logger.LogError(
                    "Failed to enable System Protection: native status 0x{Status:X8} {Message}",
                    enable.NativeStatus,
                    enable.ExceptionText
                );
                return RestorePointResult.Failed;
            }

            dialogViewModel.ProgressReporter.Report(
                new ProcessingProgress
                {
                    Message = Loc.Instance["RestorePoint.Progress.Retrying"],
                    IsIndeterminate = true,
                }
            );

            var retry = await Task.Run(() =>
                _systemRestoreService.CreateRestorePoint(Shared.RestorePointName)
            );

            if (retry.ExceptionText is not null)
                _logger.LogError("Restore point creation failed: {Message}", retry.ExceptionText);

            if (retry.Succeeded)
            {
                _logger.LogInformation("Restore point created successfully.");
                return RestorePointResult.Success;
            }

            _logger.LogError(
                "Failed to create restore point after enabling System Protection: native status 0x{Status:X8}",
                retry.NativeStatus
            );
            return RestorePointResult.Failed;
        }
        finally
        {
            // Hide() raises a routed event, so it throws when called from another thread. This
            // method deliberately does not drop the synchronization context, which keeps the
            // continuation here on the UI thread.
            dialog?.Hide();
        }
    }

    /// <summary>
    ///     Applies the specified optimization, captures revert steps into an execution scope, and
    ///     persists revert data on any successful steps.
    /// </summary>
    /// <param name="optimization">The optimization to apply.</param>
    /// <param name="progress">An <see cref="IProgress{T}"/> to report application progress.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    ///     An <see cref="OptimizationResult"/> describing the outcome, including partial-success or
    ///     failure details.
    /// </returns>
    public Task<OptimizationResult> ApplyAsync(
        IOptimization optimization,
        IProgress<ProcessingProgress> progress,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(optimization);

        return runner.RunAsync(
            new OperationRequest(
                new OperationSubject(
                    optimization.Id,
                    optimization.OptimizationKey,
                    optimization.LogName()
                ),
                optimization.Name,
                loggerFactory.CreateLogger(optimization.GetType()),
                RevertPersistence.Enabled,
                optimization.ApplyAsync
            ),
            progress,
            cancellationToken
        );
    }

    /// <summary>
    ///     Reverts the specified optimization using stored revert data from a previous apply
    ///     operation.
    /// </summary>
    /// <param name="optimization">The optimization to revert.</param>
    /// <param name="progress">
    ///     An optional <see cref="IProgress{T}"/> to report revert progress.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="RevertResult"/> describing the outcome.</returns>
    public async Task<RevertResult> RevertAsync(
        IOptimization optimization,
        IProgress<ProcessingProgress>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation(
            "Starting revert of {Name} ({Key}) with ID {Id}",
            optimization.LogName(),
            optimization.OptimizationKey,
            optimization.Id
        );

        progress?.Report(
            new ProcessingProgress
            {
                Message = Loc.Instance["Optimization.Revert.Reverting"],
                IsIndeterminate = true,
            }
        );
        var result = await revertManager
            .RevertAsync(optimization, progress, cancellationToken)
            .ConfigureAwait(false);
        progress?.Report(
            new ProcessingProgress
            {
                Message = result.Message,
                IsIndeterminate = false,
                Value = 1,
                Total = 1,
            }
        );

        if (result.Success)
            MarkRecordReverted(optimization, _logger);

        return result;
    }

    /// <summary>
    ///     Marks the item's record as reverted, keeping the steps that were undone with their
    ///     before
    ///     and after swapped. The revert file that described them is gone once the revert worked,
    ///     so the record is the only place left to see what changed back. The steps that wrote
    ///     nothing are dropped: nothing was recorded for them, so nothing was undone on their
    ///     behalf.
    /// </summary>
    private static void MarkRecordReverted(IOptimization optimization, ILogger logger)
    {
        var record = ChangeRecordStore.TryRead(optimization.Id, logger);
        record ??= ChangeRecord.From(
            new ChangeSet(),
            optimization,
            nameof(ChangeRecordOperation.Revert)
        );

        ChangeRecordStore.TryWrite(record.Reverted(DateTime.Now), logger);
    }

    /// <summary>
    ///     Updates the applied state of the specified optimizations by scanning revert data
    ///     files on disk. An optimization is considered applied when its revert JSON file exists.
    /// </summary>
    /// <param name="optimizations">The optimizations whose state to update.</param>
    public static async Task UpdateOptimizationStateAsync(params IOptimization[] optimizations)
    {
        if (optimizations.Length == 0)
            return;

        // Applied state comes from the presence of revert data, not from a database, and a
        // file that cannot be read still counts as data, so an item is never offered for a
        // fresh apply while an old payload is still sitting next to it.
        foreach (var opt in optimizations)
        {
            var applied = await RevertManager.IsAppliedAsync(opt.Id).ConfigureAwait(false);
            opt.State.IsApplied = applied;
            opt.State.AppliedAt = applied
                ? (await RevertManager.GetRevertDataAsync(opt.Id).ConfigureAwait(false))?.AppliedAt
                : null;

            // The record outlives the revert file, so it is read separately and never gates the
            // applied mark.
            var record = ChangeRecordStore.TryRead(opt.Id);
            opt.State.AppliedSummary = record is null
                ? string.Empty
                : Loc.Instance[
                    "Optimizer.UI.State.Applied.Summary",
                    record.ChangedCount,
                    record.CountOf(ChangeKind.Skip),
                    record.CountOf(ChangeKind.NotApplicable),
                    record.CountOf(ChangeKind.Refused)
                ];
        }
    }

    /// <summary>
    ///     Updates the applied state of the specified optimizations by scanning revert data
    ///     files on disk.
    /// </summary>
    /// <param name="optimizations">The optimizations whose state to update.</param>
    public static Task UpdateOptimizationStateAsync(IEnumerable<IOptimization> optimizations)
    {
        return UpdateOptimizationStateAsync(optimizations.ToArray());
    }

    /// <summary>
    ///     Retries the specified failed operation steps, optionally in reverse order. Automatically
    ///     persists recovered revert steps if a <see cref="RevertManager"/> is provided.
    /// </summary>
    /// <param name="failedSteps">The steps that failed and should be retried.</param>
    /// <param name="reverseOrder">
    ///     If <see langword="true"/>, retries steps in descending index order (useful for revert
    ///     operations).
    /// </param>
    /// <param name="logger">The logger for retry diagnostics.</param>
    /// <param name="revertManager">
    ///     Optional revert manager to persist recovered revert steps.
    /// </param>
    /// <param name="optimizationId">The optimization ID for revert step persistence.</param>
    /// <param name="optimizationKey">The optimization key for revert step persistence.</param>
    /// <param name="progress">An optional progress reporter.</param>
    /// <returns>The list of steps that remain failed after retry.</returns>
    public static async Task<List<Change>> RetryFailedStepsAsync(
        IReadOnlyList<Change> failedSteps,
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
                .ConfigureAwait(false)
        ).FailedSteps;
    }

    /// <summary>
    ///     Retries failed operation steps and returns both recovered and remaining failed steps for
    ///     detailed inspection. Automatically persists recovered revert steps if a
    ///     <see cref="RevertManager"/> is provided.
    /// </summary>
    /// <param name="failedSteps">The steps that failed and should be retried.</param>
    /// <param name="reverseOrder">
    ///     If <see langword="true"/>, retries steps in descending index order.
    /// </param>
    /// <param name="logger">The logger for retry diagnostics.</param>
    /// <param name="revertManager">
    ///     Optional revert manager to persist recovered revert steps.
    /// </param>
    /// <param name="optimizationId">The optimization ID for revert step persistence.</param>
    /// <param name="optimizationKey">The optimization key for revert step persistence.</param>
    /// <param name="progress">An optional progress reporter.</param>
    /// <returns>
    ///     A <see cref="RetryFailedStepsResult"/> containing both recovered and remaining failed
    ///     steps.
    /// </returns>
    public static async Task<RetryFailedStepsResult> RetryFailedStepsWithResultsAsync(
        IReadOnlyList<Change> failedSteps,
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

        var remainingFailedSteps = new List<Change>();
        var recoveredSteps = new List<Change>();
        var orderedSteps = reverseOrder
            ? failedSteps.OrderByDescending(s => s.Index)
            : failedSteps.OrderBy(s => s.Index);
        var total = failedSteps.Count;
        var processedCount = 0;

        progress?.Report(
            new ProcessingProgress
            {
                Message = Loc.Instance["Optimization.Retry.Processing"],
                IsIndeterminate = true,
            }
        );

        foreach (var step in orderedSteps)
        {
            processedCount++;
            progress?.Report(
                new ProcessingProgress
                {
                    Message = Loc.Instance[
                        "Optimization.RetryStep.Processing",
                        step.Name,
                        processedCount,
                        total
                    ],
                    Value = processedCount,
                    Total = total,
                }
            );

            if (step.Retry == null)
            {
                remainingFailedSteps.Add(step);
                continue;
            }

            var success = false;
            Exception? error = null;
            try
            {
                // Fresh call: retry is user-initiated, the original token may be dead. Whatever
                // the retry records is appended to the revert data below, in the order it ran.
                var retryChanges = new ChangeSet();
                var retryCall = new OpCall { Changes = retryChanges, Logger = logger };
                var retryOutcome = await step.Retry(retryCall).ConfigureAwait(false);
                success = retryOutcome.Ok;

                if (success)
                {
                    var capturedSteps = retryChanges.SuccessfulSteps;
                    var capturedStep = capturedSteps.LastOrDefault();
                    var capturedRevert = capturedStep?.Revert ?? retryOutcome.Revert;
                    var recoveredStep = new Change
                    {
                        Index = step.Index,
                        Name = capturedStep?.Name ?? step.Name,
                        Description = capturedStep?.Description ?? step.Description,
                        Kind = capturedStep?.Kind ?? step.Kind,
                        Ok = true,
                        Revert = capturedRevert,
                        Detail = capturedStep?.Detail,
                        NativeErrorCode = capturedStep?.NativeErrorCode,
                        RecordedAt = capturedStep?.RecordedAt ?? step.RecordedAt,
                        ElapsedMs = capturedStep?.ElapsedMs ?? step.ElapsedMs,
                        // The step has now run twice, and the record says so.
                        Attempt = step.Attempt + 1,
                    };

                    // A retry can recover more than one step, and each one carries the backup
                    // that makes it undoable, so all of them are persisted in the order they ran.
                    // Appending keeps the last in first out revert correct.
                    Exception? persistError = null;
                    if (revertManager != null && optimizationId.HasValue)
                    {
                        var compensations = capturedSteps
                            .Where(c => c.Revert != null)
                            .Select(c => c.Revert!)
                            .ToList();
                        if (compensations.Count == 0 && recoveredStep.Revert != null)
                            compensations.Add(recoveredStep.Revert);

                        foreach (var compensation in compensations)
                        {
                            try
                            {
                                await revertManager
                                    .AppendRevertStepAsync(
                                        optimizationId.Value,
                                        optimizationKey ?? string.Empty,
                                        compensation
                                    )
                                    .ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                persistError = ex;
                                logger.LogError(
                                    ex,
                                    "Failed to auto-persist recovered revert step for {Index}",
                                    step.Index
                                );
                            }
                        }
                    }

                    // The step ran, but the machine cannot be undone for it: reporting it as
                    // recovered would claim coverage that is not on disk, so it comes back as a
                    // failed step naming why its compensation could not be written.
                    if (persistError is null)
                        recoveredSteps.Add(recoveredStep);
                    else
                        remainingFailedSteps.Add(
                            recoveredStep with
                            {
                                Ok = false,
                                Error = persistError.Message,
                                ErrorDetail = persistError.ToString(),
                            }
                        );
                }
            }
            catch (Exception ex)
            {
                error = ex;
                logger.LogError(ex, "Retry step {Name} failed", step.Name);
            }

            if (!success)
                remainingFailedSteps.Add(
                    error != null
                        ? step with
                        {
                            Error = error.Message,
                            Attempt = step.Attempt + 1,
                        }
                        : step with
                        {
                            Attempt = step.Attempt + 1,
                        }
                );
        }

        // Best effort, like the record itself: what the retry reached is written back, and a record
        // that cannot be written changes neither the retry result nor the outcome already reported.
        if (optimizationId.HasValue)
            TryRecordRetry(optimizationId.Value, recoveredSteps, remainingFailedSteps, logger);

        return new RetryFailedStepsResult(remainingFailedSteps, recoveredSteps);
    }

    /// <summary>
    ///     Writes what a retry changed into the record of the run, never throwing: the record is a
    ///     report, and a report that cannot be written is not a reason to fail the retry.
    /// </summary>
    private static void TryRecordRetry(
        Guid optimizationId,
        IReadOnlyList<Change> recovered,
        IReadOnlyList<Change> stillFailed,
        ILogger logger
    )
    {
        try
        {
            var record = ChangeRecordStore.TryRead(optimizationId, logger);
            if (record is null)
                return;

            ChangeRecordStore.TryWrite(record.Retried(recovered, stillFailed), logger);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record the retry for {Id}", optimizationId);
        }
    }

    /// <summary>
    ///     Deletes all files in the downloads directory. Silently skips files that cannot be
    ///     deleted.
    /// </summary>
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
    Success,

    Failed,

    /// <summary>
    ///     The creation was skipped because a restore point was already created within the
    ///     frequency limit (24 hours).
    /// </summary>
    FrequencyLimitReached,
}

/// <summary>
///     Represents the result of retrying failed operation steps, separating recovered steps from
///     those that remain failed.
/// </summary>
/// <param name="FailedSteps">The steps that remain failed after retry.</param>
/// <param name="RecoveredSteps">The steps that succeeded on retry.</param>
public sealed record RetryFailedStepsResult(List<Change> FailedSteps, List<Change> RecoveredSteps);
