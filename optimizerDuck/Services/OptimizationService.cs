using System.IO;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Optimization;
using optimizerDuck.Core.Models.Revert;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Managers;

namespace optimizerDuck.Services;

public class OptimizationService(
    RevertManager revertManager,
    ILoggerFactory loggerFactory,
    SystemInfoService systemInfoService,
    StreamService streamService)
{
    private static readonly Dictionary<Guid, (bool IsApplied, DateTime? AppliedAt)> _stateCache = new();
    private static readonly object _cacheLock = new();

    #region Main Methods

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
                    new OptimizationContext(optimizationLogger, systemInfoService.Snapshot, streamService)));

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
                {
                    lock (_cacheLock)
                    {
                        _stateCache.Remove(optimization.Id);
                    }
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
                        : string.Format(Translations.Optimization_Apply_Error_FailedWithSteps, optimization.OptimizationKey,
                            failedSteps.Count),
                FailedSteps = failedSteps
            };

            // Update cache after successful apply
            if (status == OptimizationSuccessResult.Success)
            {
                lock (_cacheLock)
                {
                    _stateCache[optimization.Id] = (true, DateTime.Now);
                }
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
                    optimization.OptimizationKey, ex.Message),
                Exception = ex,
                FailedSteps = failedSteps
            };

            // Best-effort: persist any revert steps that were recorded before the exception.
            if (revertContext is not null && !revertContextDisposed)
            {
                try
                {
                    await revertContext.DisposeAsync();
                    revertContextDisposed = true;
                }
                catch
                {
                    // Best-effort cleanup only.
                }
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

        var steps = tracker.GetSteps().ToList();
        result.FailedStepDetails = steps
            .Where(s => s.Error != null)
            .ToList();

        progress?.Report(new ProcessingProgress
        {
            Message = result.Message,
            IsIndeterminate = false,
            Value = 1,
            Total = 1
        });

        // Update cache after revert
        if (result.Success)
        {
            lock (_cacheLock)
            {
                _stateCache.Remove(optimization.Id);
            }
        }

        return result;
    }

    #endregion Main Methods

    #region Helpers

    public static Task<bool> IsAppliedAsync(Guid optimizationId)
    {
        return RevertManager.IsAppliedAsync(optimizationId);
    }

    public static async Task UpdateOptimizationStateAsync(params IOptimization[] optimizations)
    {
        var optimizationIds = optimizations.Select(o => o.Id).ToArray();
        var uncachedIds = new List<Guid>();

        lock (_cacheLock)
        {
            foreach (var optimization in optimizations)
            {
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
        }

        // Only fetch from disk for uncached optimizations
        if (uncachedIds.Count > 0)
        {
            var tasks = uncachedIds.Select(async id =>
            {
                var revertData = await RevertManager.GetRevertDataAsync(id);
                var state = (IsApplied: revertData != null, AppliedAt: revertData?.AppliedAt);

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

    public static async Task UpdateOptimizationStateAsync(IEnumerable<IOptimization> optimizations)
    {
        await UpdateOptimizationStateAsync(optimizations.ToArray());
    }

    public static async Task<List<OperationStepResult>> RetryFailedStepsAsync(
        IReadOnlyList<OperationStepResult> failedSteps,
        bool reverseOrder,
        IProgress<ProcessingProgress>? progress = null)
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

            var success = await step.RetryAction();
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