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
    private static readonly Dictionary<Guid, (bool IsApplied, DateTime? AppliedAt)> _stateCache = new();
    private static readonly object _cacheLock = new();
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

    public async Task<OptimizationResult> ApplyAsync(IOptimization optimization,
        IProgress<ProcessingProgress> progress)
    {
        var optimizationLogger = loggerFactory.CreateLogger(optimization.GetType());

        using var executionScope = ExecutionScope.Begin(optimization.Id, optimization.OptimizationKey,
            optimizationLogger);

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

            if (result.Status == OptimizationSuccessResult.Success)
                lock (_cacheLock)
                {
                    _stateCache[optimization.Id] = (true, DateTime.Now);
                }

            if (executionScope.HasSuccessfulSteps)
            {
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

            var status = GetStatus(executionScope);
            result = new OptimizationResult
            {
                Status = status,
                Message = string.Format(Translations.Optimization_Apply_Error_Failed,
                    optimization.OptimizationKey),
                Exception = ex,
                FailedSteps = failedStepResults
            };

            if (executionScope.HasSuccessfulSteps)
            {
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
        }

        return result;

        OptimizationSuccessResult GetStatus(Core.Models.Execution.ExecutionScope scope)
        {
            if (scope.ExecutedSteps.Count == 0)
                return OptimizationSuccessResult.Failed;

            var failed = scope.FailedSteps.Count;
            var total = scope.ExecutedSteps.Count;

            return failed switch
            {
                0 => OptimizationSuccessResult.Success,
                _ when failed < total => OptimizationSuccessResult.PartialSuccess,
                _ => OptimizationSuccessResult.Failed
            };
        }
    }

    public async Task<RevertResult> RevertAsync(IOptimization optimization,
        IProgress<ProcessingProgress>? progress = null)
    {
        var optimizationLogger = loggerFactory.CreateLogger(optimization.GetType());
        using var scope = ExecutionScope.Begin(optimizationLogger);

        progress?.Report(new ProcessingProgress
        {
            Message = Translations.Optimization_Revert_Reverting,
            IsIndeterminate = true
        });

        var result = await revertManager.RevertAsync(
            optimization.Id,
            optimization.OptimizationKey,
            progress);

        var scopeSteps = scope.GetStepResults();
        if (result.FailedStepDetails.Count == 0 && scopeSteps.Any(s => !s.Success))
            result.FailedStepDetails = scopeSteps.Where(s => !s.Success).ToList();

        progress?.Report(new ProcessingProgress
        {
            Message = result.Message,
            IsIndeterminate = false,
            Value = 1,
            Total = 1
        });

        if (result.Success)
            lock (_cacheLock)
            {
                _stateCache.Remove(optimization.Id);
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
                if (_stateCache.TryGetValue(optimization.Id, out var cachedState))
                {
                    optimization.State.IsApplied = cachedState.IsApplied;
                    optimization.State.AppliedAt = cachedState.AppliedAt;
                }
                else
                {
                    uncachedIds.Add(optimization.Id);
                }
        }

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
                }

                if (attempt < maxRetries - 1)
                    await Task.Delay(200 * (attempt + 1));
            }

            if (!success)
                stillFailed.Add(step);
        }

        return stillFailed;
    }

    public static void ClearResources()
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