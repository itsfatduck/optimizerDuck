using System.Collections.ObjectModel;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Optimization;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services;
using optimizerDuck.UI.ViewModels.Dialogs;
using optimizerDuck.UI.Views.Dialogs;
using Wpf.Ui;
using Wpf.Ui.Controls;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace optimizerDuck.UI.ViewModels.Optimizer;

public partial class OptimizationCategoryViewModel : ViewModel
{
    private readonly IOptimizationCategory _category;
    private readonly IContentDialogService _contentDialogService;
    private readonly ILogger<OptimizationCategoryViewModel> _logger;
    private readonly OptimizationService _optimizationService;
    private readonly ISnackbarService _snackbarService;

    private bool _isApplied;

    [ObservableProperty] private ObservableCollection<IOptimization> _optimizations = [];

    public OptimizationCategoryViewModel(
        IOptimizationCategory category,
        OptimizationService optimizationService,
        ISnackbarService snackbarService,
        IContentDialogService contentDialogService,
        ILogger<OptimizationCategoryViewModel> logger)
    {
        _category = category;
        _optimizationService = optimizationService;
        _snackbarService = snackbarService;
        _logger = logger;
        _contentDialogService = contentDialogService;
    }

    public override async Task OnNavigatedToAsync()
    {
        await LoadOptimizationStatesAsync();
    }

    private async Task LoadOptimizationStatesAsync()
    {
        if (Optimizations.Count > 0) return;

        foreach (var optimization in _category.Optimizations)
        {
            Optimizations.Add(optimization);
            await Task.Delay(10);
        }
    }

    // Keep the progress dialog visible while long-running work is running.
    private async Task<T> RunWithProcessingDialogAsync<T>(
        IOptimization optimization,
        Func<IProgress<ProcessingProgress>, Task<T>> action)
    {
        var viewModel = new ProcessingViewModel();
        var dialog = new ContentDialog
        {
            Title = BuildDialogTitle(optimization),
            Content = new ProcessingDialog { DataContext = viewModel },
            IsFooterVisible = false
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

    private async Task<RetryOutcome> HandleFailedStepsAsync(IOptimization optimization,
        List<OperationStepResult> failedSteps, bool isRevert)
    {
        if (failedSteps.Count == 0)
            return RetryOutcome.None;

        // Always keep steps ordered by their original index for a stable UI.
        var currentFailed = failedSteps.OrderBy(s => s.Index).ToList();
        while (currentFailed.Count > 0)
        {
            var dialogViewModel = new OptimizationResultDialogViewModel(currentFailed);
            var dialogContent = new OptimizationResultDialog { DataContext = dialogViewModel };

            var dialog = new ContentDialog
            {
                Title = BuildDialogTitle(optimization),
                Content = dialogContent,
                PrimaryButtonText = Translations.Button_Retry,
                CloseButtonText = Translations.Button_Cancel
            };

            var result = await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
            if (result != ContentDialogResult.Primary)
                return RetryOutcome.Skipped;

            // Retry only the remaining failed steps. For revert, keep reverse order.
            if (isRevert)
            {
                currentFailed = (await RunWithProcessingDialogAsync(
                        optimization,
                        progress => OptimizationService.RetryFailedStepsAsync(
                            currentFailed,
                            true,
                            progress)))
                    .OrderByDescending(s => s.Index)
                    .ToList();

                if (currentFailed.Count == 0)
                {
                    await OptimizationService.UpdateOptimizationStateAsync(optimization);
                    return RetryOutcome.Succeeded;
                }
            }
            else
            {
                currentFailed = (await RunWithProcessingDialogAsync(
                        optimization,
                        progress => OptimizationService.RetryFailedStepsAsync(
                            currentFailed,
                            false,
                            progress)))
                    .OrderBy(s => s.Index)
                    .ToList();

                if (currentFailed.Count == 0) return RetryOutcome.Succeeded;
            }
        }

        return RetryOutcome.Succeeded;
    }

    private async Task<bool> HandleRestorePointAsync()
    {
        var dialogContent = new RestorePointDialog();
        var dialog = new ContentDialog
        {
            Title = Translations.RestorePoint_Title,
            Content = dialogContent,

            PrimaryButtonText = Translations.Button_Ok,
            SecondaryButtonText = Translations.Button_Skip,
            CloseButtonText = Translations.Button_Cancel
        };

        var result = await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
        if (result == ContentDialogResult.None) // User cancelled
            return false;

        if (result == ContentDialogResult.Secondary) // User skipped
            return true;

        var success = await _optimizationService.CreateRestorePointAsync();
        if (!success)
            _snackbarService.Show(
                Translations.RestorePoint_Snackbar_Error_Title,
                Translations.RestorePoint_Snackbar_Error_Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
        else
            _snackbarService.Show(
                Translations.RestorePoint_Snackbar_Success_Title,
                string.Format(Translations.RestorePoint_Snackbar_Success_Message, Shared.RestorePointName),
                ControlAppearance.Success,
                new SymbolIcon { Symbol = SymbolRegular.CheckmarkCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );

        return true;
    }

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
                    Text = optimization.Id.ToString()
                },
                new TextBlock
                {
                    FontTypography = FontTypography.Subtitle,
                    Text = optimization.Name
                }
            }
        };
    }

    private enum RetryOutcome
    {
        None,
        Succeeded,
        Skipped
    }

    #region Commands

    [RelayCommand]
    private async Task ToggleOptimizationAsync(IOptimization optimization)
    {
        // Keep a stable reference to the previous state in case we need to roll back UI changes.
        var wasApplied = await OptimizationService.IsAppliedAsync(optimization.Id);

        if (!_isApplied)
        {
            if (!await HandleRestorePointAsync())
            {
                optimization.State.IsApplied = wasApplied;
                return;
            }

            _isApplied = true;
        }

        try
        {
            // Apply optimization if not already applied
            if (!wasApplied)
            {
                _logger.LogInformation("===== START applying optimization {OptimizationName} ({OptimizationId}) =====",
                    optimization.OptimizationKey, optimization.Id);
                var applyResult = await RunWithProcessingDialogAsync(
                    optimization,
                    progress => _optimizationService.ApplyAsync(optimization, progress));

                await OptimizationService.UpdateOptimizationStateAsync(optimization);

                // If result status is Failed, we can't retry it
                // We just need to show the error message and let the user know
                // It's "not possible" to retry failed steps and save revert data again
                if (applyResult.Status == OptimizationSuccessResult.Failed)
                {
                    _snackbarService.Show(
                        Translations.Optimization_Apply_Snackbar_Error_Title,
                        applyResult.Message,
                        ControlAppearance.Danger,
                        new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                        TimeSpan.FromSeconds(5));
                    return;
                }

                // Retry only if there are some successful steps and some failed steps
                var retryOutcome = await HandleFailedStepsAsync(
                    optimization,
                    applyResult.FailedSteps,
                    false);

                // If applied successfully or retry succeeded, mark as applied
                var succeeded = applyResult.Status == OptimizationSuccessResult.Success ||
                                retryOutcome == RetryOutcome.Succeeded;

                // If retry skipped, mark as partially applied
                var partial = retryOutcome == RetryOutcome.Skipped;

                // If retry failed, mark as failed
                var failed = retryOutcome == RetryOutcome.None &&
                             applyResult.Status != OptimizationSuccessResult.Success;

                // If retry skipped or failed, mark as failed
                if (failed)
                    _snackbarService.Show(
                        Translations.Optimization_Apply_Snackbar_Error_Title,
                        applyResult.Message,
                        ControlAppearance.Danger,
                        new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                        TimeSpan.FromSeconds(5));

                if (partial)
                    _snackbarService.Show(
                        Translations.Optimization_Apply_Snackbar_Error_Title,
                        applyResult.Message,
                        ControlAppearance.Caution,
                        new SymbolIcon { Symbol = SymbolRegular.Warning24, Filled = true },
                        TimeSpan.FromSeconds(5));

                if (applyResult.Exception != null)
                    _logger.LogError(applyResult.Exception, "Failed to apply {Name}", optimization.OptimizationKey);
                else if (succeeded)
                    _logger.LogInformation("Successfully applied {Name}", optimization.OptimizationKey);
                else if (partial)
                    _logger.LogWarning("Partially applied {Name}", optimization.OptimizationKey);
                else
                    _logger.LogWarning("Failed to apply {Name}", optimization.OptimizationKey);

                _logger.LogInformation("===== END applying optimization {OptimizationName} ({OptimizationId}) =====",
                    optimization.OptimizationKey, optimization.Id);
            }
            else
            {
                // Revert optimization if already applied
                _logger.LogInformation("===== START reverting optimization {OptimizationName} ({OptimizationId}) =====",
                    optimization.OptimizationKey, optimization.Id);

                var revertResult = await RunWithProcessingDialogAsync(
                    optimization,
                    progress => _optimizationService.RevertAsync(optimization, progress));

                // Refresh state after revert attempt. Failed steps can still be retried in the current session.
                await OptimizationService.UpdateOptimizationStateAsync(optimization);

                var retryOutcome = await HandleFailedStepsAsync(
                    optimization,
                    revertResult.FailedStepDetails,
                    true);

                var succeeded = revertResult.Success || retryOutcome == RetryOutcome.Succeeded;
                var partial = retryOutcome == RetryOutcome.Skipped;
                var failed = retryOutcome == RetryOutcome.None && !revertResult.Success;

                if (failed)
                    _snackbarService.Show(
                        Translations.Optimization_Revert_Snackbar_Error_Title,
                        revertResult.Message,
                        ControlAppearance.Danger,
                        new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                        TimeSpan.FromSeconds(5));

                if (partial)
                    _snackbarService.Show(
                        Translations.Optimization_Revert_Snackbar_Error_Title,
                        revertResult.Message,
                        ControlAppearance.Caution,
                        new SymbolIcon { Symbol = SymbolRegular.Warning24, Filled = true },
                        TimeSpan.FromSeconds(5));

                if (revertResult.Exception != null)
                    _logger.LogError(revertResult.Exception, "Failed to revert {Name}", optimization.OptimizationKey);
                else if (succeeded)
                    _logger.LogInformation("Successfully reverted {Name}", optimization.OptimizationKey);
                else if (partial)
                    _logger.LogWarning("Partially reverted {Name}", optimization.OptimizationKey);
                else
                    _logger.LogWarning("Failed to revert {Name}", optimization.OptimizationKey);
                _logger.LogInformation("===== END reverting optimization {OptimizationName} ({OptimizationId}) =====",
                    optimization.OptimizationKey, optimization.Id);
            }
        }
        catch (Exception ex)
        {
            optimization.State.IsApplied = wasApplied; // revert UI state on failure
            _logger.LogError(ex, "Failed to toggle optimization {Name}", optimization.OptimizationKey);
            _snackbarService.Show(
                Translations.Optimization_Toggle_Snackbar_Error_Title,
                string.Format(Translations.Optimization_Toggle_Snackbar_Error_Message, ex.Message),
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
        }
    }

    [RelayCommand]
    private async Task ShowDetailsAsync(IOptimization optimization)
    {
        var dialogViewModel = new OptimizationDetailsViewModel(optimization, _snackbarService, _logger);
        var dialogContent = new OptimizationDetailsDialog { DataContext = dialogViewModel };
        var dialog = new ContentDialog
        {
            Title = BuildDialogTitle(optimization),
            Content = dialogContent,
            CloseButtonText = Translations.Button_Ok
        };
        var result = await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
    }

    #endregion Commands
}