namespace optimizerDuck.UI.ViewModels.Optimizer;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Configuration;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Revert;
using optimizerDuck.Domain.UI;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Conditions;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.Optimization;
using optimizerDuck.Services.Revert;
using optimizerDuck.Services.System;
using optimizerDuck.UI.Dialogs;
using optimizerDuck.UI.ViewModels.Dialogs;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using TextBlock = Wpf.Ui.Controls.TextBlock;

public partial class OptimizationCategoryViewModel
{
    /// <summary>
    ///     Run a long-running action with a processing dialog.
    /// </summary>
    private async Task<T> RunWithProcessingDialogAsync<T>(
        IOptimization optimization,
        Func<IProgress<ProcessingProgress>, Task<T>> action
    )
    {
        var viewModel = new ProcessingViewModel();
        var dialog = new ContentDialog
        {
            Title = BuildDialogTitle(optimization),
            Content = new ProcessingDialog { DataContext = viewModel },
            IsFooterVisible = false,
        };

        _ = _contentDialogService.ShowAsync(dialog, CancellationToken.None);

        try
        {
            return await action(viewModel.ProgressReporter);
        }
        finally
        {
            dialog.Hide();
        }
    }

    /// <summary>
    ///     Handle failed steps for an optimization.
    /// </summary>
    private async Task<FailureResolutionOutcome> HandleRetryableFailuresAsync(
        IOptimization optimization,
        IReadOnlyList<Change> failedSteps,
        OptimizationOperation operation
    )
    {
        if (failedSteps.Count == 0)
            return FailureResolutionOutcome.NoFailures;

        var remainingFailedSteps = failedSteps.OrderBy(s => s.Index).ToList();
        while (remainingFailedSteps.Count > 0)
        {
            // Only offer Retry when at least one failed step can actually be re-run. A
            // non-retryable failure (e.g. access denied on a Windows-protected service)
            // must not loop the dialog with a button that cannot change anything.
            var canRetry = remainingFailedSteps.Any(s => s.Retry != null);
            var dialogViewModel = new OptimizationResultDialogViewModel(
                remainingFailedSteps,
                operation == OptimizationOperation.Revert
                    ? ChangeRecordOperation.Revert
                    : ChangeRecordOperation.Apply
            );
            var dialogContent = new OptimizationResultDialog { DataContext = dialogViewModel };

            var dialog = new ContentDialog
            {
                Title = BuildDialogTitle(optimization),
                Content = dialogContent,
            };
            if (canRetry)
            {
                BindLocalized(dialog, ContentDialog.PrimaryButtonTextProperty, "Button.Retry");
                BindLocalized(dialog, ContentDialog.CloseButtonTextProperty, "Button.Cancel");
            }
            else
            {
                BindLocalized(dialog, ContentDialog.CloseButtonTextProperty, "Button.Ok");
            }

            var result = await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
            if (!canRetry || result != ContentDialogResult.Primary)
                return FailureResolutionOutcome.Deferred;

            if (operation == OptimizationOperation.Revert)
            {
                var retryResult = await RunWithProcessingDialogAsync(
                    optimization,
                    progress =>
                        OptimizationService.RetryFailedStepsWithResultsAsync(
                            remainingFailedSteps,
                            true,
                            _logger,
                            revertManager: null,
                            optimizationId: null,
                            optimizationKey: null,
                            progress: progress
                        )
                );

                foreach (var recoveredStep in retryResult.RecoveredSteps)
                {
                    await _revertManager.RemoveRevertStepAtIndexAsync(
                        optimization.Id,
                        optimization.OptimizationKey,
                        recoveredStep.Index
                    );
                }

                remainingFailedSteps = retryResult.FailedSteps.OrderBy(s => s.Index).ToList();
                if (remainingFailedSteps.Count == 0)
                {
                    _revertManager.RemoveRevertData(optimization.Id, optimization.OptimizationKey);
                    await OptimizationService.UpdateOptimizationStateAsync(optimization);
                    return FailureResolutionOutcome.Recovered;
                }
            }
            else
            {
                var retryResult = await RunWithProcessingDialogAsync(
                    optimization,
                    progress =>
                        OptimizationService.RetryFailedStepsWithResultsAsync(
                            remainingFailedSteps,
                            false,
                            _logger,
                            _revertManager,
                            optimization.Id,
                            optimization.OptimizationKey,
                            progress
                        )
                );

                var newFailed = retryResult.FailedSteps.OrderBy(s => s.Index).ToList();

                remainingFailedSteps = newFailed;
                if (remainingFailedSteps.Count == 0)
                    return FailureResolutionOutcome.Recovered;
            }
        }

        return FailureResolutionOutcome.Recovered;
    }

    private static OperationNotificationState ResolveApplyNotificationState(
        OptimizationResult applyResult,
        FailureResolutionOutcome retryOutcome
    )
    {
        if (
            applyResult.Status == OptimizationSuccessResult.Success
            || retryOutcome == FailureResolutionOutcome.Recovered
        )
            return OperationNotificationState.Success;

        if (applyResult.Status == OptimizationSuccessResult.NothingToDo)
            return OperationNotificationState.NothingToDo;

        return retryOutcome == FailureResolutionOutcome.Deferred
            ? OperationNotificationState.Partial
            : OperationNotificationState.Failed;
    }

    private static OperationNotificationState ResolveRevertNotificationState(
        RevertResult revertResult,
        FailureResolutionOutcome retryOutcome
    )
    {
        if (revertResult.Success || retryOutcome == FailureResolutionOutcome.Recovered)
            return OperationNotificationState.Success;

        return revertResult.AllStepsFailed
            ? OperationNotificationState.Failed
            : OperationNotificationState.Partial;
    }

    /// <summary>
    ///     Shows a snackbar notification based on the operation outcome.
    /// </summary>
    private void ShowOperationOutcomeSnackbar(
        OperationNotificationState notificationState,
        OptimizationOperation operation,
        string message,
        bool restorePointCreated = false
    )
    {
        var showSuccess = _appOptionsMonitor.CurrentValue.Optimize.ShowCompletionNotification;
        var finalMessage = message;

        if (restorePointCreated && showSuccess)
            finalMessage +=
                "\n"
                + Loc.Instance["RestorePoint.Snackbar.Success.Message", Shared.RestorePointName];

        if (notificationState == OperationNotificationState.NothingToDo)
        {
            // Respects the completion notification setting like the success path, because the
            // card already carries the mark when the toast is turned off.
            if (showSuccess)
                _snackbarService.Show(
                    Loc.Instance["Optimization.Apply.Snackbar.NothingToDo.Title"],
                    finalMessage,
                    ControlAppearance.Info,
                    new SymbolIcon { Symbol = SymbolRegular.Info24, Filled = true },
                    TimeSpan.FromSeconds(5)
                );
        }
        else if (notificationState == OperationNotificationState.Success)
        {
            if (showSuccess)
                _snackbarService.Show(
                    operation == OptimizationOperation.Apply
                        ? Loc.Instance["Optimization.Apply.Snackbar.Success.Title"]
                        : Loc.Instance["Optimization.Revert.Snackbar.Success.Title"],
                    finalMessage,
                    ControlAppearance.Success,
                    new SymbolIcon { Symbol = SymbolRegular.CheckmarkCircle24, Filled = true },
                    TimeSpan.FromSeconds(5)
                );
        }
        else if (notificationState == OperationNotificationState.Partial)
        {
            _snackbarService.Show(
                operation == OptimizationOperation.Apply
                    ? Loc.Instance["Optimization.Apply.Snackbar.Error.Title"]
                    : Loc.Instance["Optimization.Revert.Snackbar.Error.Title"],
                finalMessage,
                ControlAppearance.Caution,
                new SymbolIcon { Symbol = SymbolRegular.Warning24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
        }
        else if (notificationState == OperationNotificationState.Failed)
        {
            _snackbarService.Show(
                operation == OptimizationOperation.Apply
                    ? Loc.Instance["Optimization.Apply.Snackbar.Error.Title"]
                    : Loc.Instance["Optimization.Revert.Snackbar.Error.Title"],
                finalMessage,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
        }
    }

    /// <summary>
    ///     Handles the restore point dialog.
    /// </summary>
    private async Task<(bool Proceed, bool RestorePointCreated)> HandleRestorePointAsync()
    {
        var dialogContent = new RestorePointDialog();
        var dialog = new ContentDialog { Content = dialogContent };
        BindLocalized(dialog, ContentDialog.TitleProperty, "RestorePoint.Title");
        BindLocalized(dialog, ContentDialog.PrimaryButtonTextProperty, "Button.Ok");
        BindLocalized(dialog, ContentDialog.SecondaryButtonTextProperty, "Button.Skip");
        BindLocalized(dialog, ContentDialog.CloseButtonTextProperty, "Button.Cancel");

        var result = await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
        if (result == ContentDialogResult.None)
        {
            _logger.LogInformation("User cancelled the restore point dialog");
            return (false, false);
        }

        if (result == ContentDialogResult.Secondary)
        {
            _logger.LogInformation("User chose to skip creating a restore point");
            return (true, false);
        }

        try
        {
            _logger.LogInformation("User accepted to create a restore point, starting creation");
            var resultState = await _optimizationService.CreateRestorePointAsync();

            switch (resultState)
            {
                case RestorePointResult.Success:
                    if (!_appOptionsMonitor.CurrentValue.Optimize.ShowCompletionNotification)
                    {
                        _snackbarService.Show(
                            Loc.Instance["RestorePoint.Snackbar.Success.Title"],
                            Loc.Instance[
                                "RestorePoint.Snackbar.Success.Message",
                                Shared.RestorePointName
                            ],
                            ControlAppearance.Success,
                            new SymbolIcon
                            {
                                Symbol = SymbolRegular.CheckmarkCircle24,
                                Filled = true,
                            },
                            TimeSpan.FromSeconds(5)
                        );
                    }

                    _logger.LogInformation("Successfully created restore point");
                    return (true, true);

                case RestorePointResult.FrequencyLimitReached:
                    _snackbarService.Show(
                        Loc.Instance["RestorePoint.Snackbar.Error.Title"],
                        Loc.Instance["RestorePoint.Snackbar.Warning.LimitReached"],
                        ControlAppearance.Caution,
                        new SymbolIcon { Symbol = SymbolRegular.Warning24, Filled = true },
                        TimeSpan.FromSeconds(5)
                    );
                    _logger.LogWarning("Restore point creation frequency limit reached");
                    break;

                case RestorePointResult.Failed:
                default:
                    _snackbarService.Show(
                        Loc.Instance["RestorePoint.Snackbar.Error.Title"],
                        Loc.Instance["RestorePoint.Snackbar.Error.Message"],
                        ControlAppearance.Danger,
                        new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                        TimeSpan.FromSeconds(5)
                    );
                    _logger.LogError("Failed to create restore point");
                    break;
            }

            var failedMessage =
                resultState == RestorePointResult.FrequencyLimitReached
                    ? Loc.Instance["RestorePoint.Snackbar.Warning.LimitReached"]
                    : "";
            var failedResult = await _contentDialogService.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions
                {
                    Title = Loc.Instance["RestorePoint.Snackbar.Error.Title"],
                    Content =
                        failedMessage + $"\n{Loc.Instance["RestorePoint.Snackbar.Error.Message"]}",
                    PrimaryButtonText = Loc.Instance["Button.Skip"],
                    CloseButtonText = Loc.Instance["Button.Cancel"],
                },
                CancellationToken.None
            );

            if (failedResult == ContentDialogResult.Primary)
                return (true, false);

            return (false, false);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to create restore point");
            _snackbarService.Show(
                Loc.Instance["RestorePoint.Snackbar.Error.Title"],
                Loc.Instance["RestorePoint.Snackbar.Error.Message"],
                ControlAppearance.Caution,
                new SymbolIcon { Symbol = SymbolRegular.Warning24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
        }

        return (true, false);
    }

    /// <summary>Binds a dialog property to a localization key so it follows runtime language changes.</summary>
    private static void BindLocalized(ContentDialog dialog, DependencyProperty property, string key)
    {
        dialog.SetBinding(
            property,
            new Binding($"[{key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay }
        );
    }

    /// <summary>
    ///     Builds the dialog title for an optimization.
    /// </summary>
    private static StackPanel BuildDialogTitle(IOptimization optimization)
    {
        return new StackPanel
        {
            Children =
            {
                new TextBlock
                {
                    FontTypography = FontTypography.Caption,
                    Appearance = TextColor.Disabled,
                    Text = optimization.Id.ToString(),
                },
                new TextBlock
                {
                    FontTypography = FontTypography.Subtitle,
                    Text = optimization.Name,
                },
            },
        };
    }

    private enum FailureResolutionOutcome
    {
        NoFailures,
        Recovered,
        Deferred,
    }

    private enum OperationNotificationState
    {
        Success,
        Partial,
        Failed,
        NothingToDo,
    }

    private enum OptimizationOperation
    {
        Apply,
        Revert,
    }
}
