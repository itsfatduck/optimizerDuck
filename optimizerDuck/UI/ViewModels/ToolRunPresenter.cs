using Microsoft.Extensions.Logging;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.Optimization;
using optimizerDuck.UI.Dialogs;
using optimizerDuck.UI.ViewModels.Dialogs;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels;

/// <summary>
///     Runs one tool action and reports the outcome to the user, offering a retry for failed steps.
/// </summary>
/// <remarks>Shared by the tool pages, so a tool action is run and reported one way.</remarks>
public class ToolRunPresenter(
    OperationRunner runner,
    IContentDialogService contentDialogService,
    ISnackbarService snackbarService,
    ILogger<ToolRunPresenter> logger
)
{
    /// <summary>
    ///     Runs the request while a progress dialog is shown. A dialog that cannot be built does
    ///     not stop the run.
    /// </summary>
    /// <param name="request">The run to execute.</param>
    /// <returns>The classified outcome of the run.</returns>
    public async Task<OptimizationResult> RunAsync(OperationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var progressViewModel = new ProcessingViewModel();
        var dialog = TryCreateProgressDialog(request, progressViewModel);
        if (dialog is not null)
            _ = ShowProgressAsync(dialog);

        try
        {
            return await runner.RunAsync(request, progressViewModel.ProgressReporter);
        }
        finally
        {
            if (dialog is not null)
            {
                try
                {
                    dialog.Hide();
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Could not hide the progress dialog");
                }
            }
        }
    }

    /// <summary>
    ///     Reports the outcome to the user: the failed steps get a retry dialog, and a failure with
    ///     no retryable step is shown in a snackbar.
    /// </summary>
    /// <param name="request">The run that produced the outcome.</param>
    /// <param name="result">The outcome of the run.</param>
    /// <param name="failureTitle">The heading a failure is shown under.</param>
    /// <returns>
    ///     <see langword="true" /> if the action ended up as the user asked; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    public async Task<bool> ReportAsync(
        OperationRequest request,
        OptimizationResult result,
        string failureTitle
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        if (result.FailedSteps.Count > 0)
            return await OfferRetryAsync(request, result.FailedSteps);

        if (result.Status == OptimizationSuccessResult.Failed)
        {
            ShowFailure(failureTitle, result.Message);
            return false;
        }

        return true;
    }

    /// <summary>Shows the failed steps and retries them while the user asks for it.</summary>
    /// <param name="request">The run whose steps failed.</param>
    /// <param name="failedSteps">The steps that failed.</param>
    /// <returns>Whether everything the run attempted ended up done.</returns>
    private async Task<bool> OfferRetryAsync(
        OperationRequest request,
        IReadOnlyList<Change> failedSteps
    )
    {
        var remaining = failedSteps.OrderBy(step => step.Index).ToList();

        while (remaining.Count > 0)
        {
            var canRetry = remaining.Any(step => step.Retry != null);
            var dialog = new ContentDialog
            {
                Title = request.DisplayName,
                Content = new OptimizationResultDialog
                {
                    DataContext = new OptimizationResultDialogViewModel(
                        remaining,
                        ChangeRecordOperation.Apply
                    ),
                },
                CloseButtonText = Loc.Instance[canRetry ? "Button.Cancel" : "Button.Ok"],
            };
            if (canRetry)
                dialog.PrimaryButtonText = Loc.Instance["Button.Retry"];

            var answer = await contentDialogService.ShowAsync(dialog, CancellationToken.None);
            if (!canRetry || answer != ContentDialogResult.Primary)
                return false;

            var retry = await RetryAsync(request, remaining);

            remaining = retry.FailedSteps.OrderBy(step => step.Index).ToList();
        }

        return true;
    }

    /// <summary>
    ///     Retries the failed steps through the same lifecycle and reports what the retry recovered
    ///     and what still failed. Kept apart from the dialog, so a retry needs no shell.
    /// </summary>
    /// <param name="request">The run whose steps failed.</param>
    /// <param name="failedSteps">The steps to retry.</param>
    /// <returns>What the retry recovered and what still failed.</returns>
    internal Task<RetryFailedStepsResult> RetryAsync(
        OperationRequest request,
        IReadOnlyList<Change> failedSteps
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(failedSteps);

        return OptimizationService.RetryFailedStepsWithResultsAsync(
            failedSteps,
            reverseOrder: false,
            logger,
            revertManager: null,
            optimizationId: request.Subject.Id,
            optimizationKey: request.Subject.Key
        );
    }

    /// <summary>
    ///     Builds the progress dialog, or <see langword="null" /> when the shell cannot build it.
    /// </summary>
    private ContentDialog? TryCreateProgressDialog(
        OperationRequest request,
        ProcessingViewModel progressViewModel
    )
    {
        try
        {
            return new ContentDialog
            {
                Title = request.DisplayName,
                Content = new ProcessingDialog { DataContext = progressViewModel },
                IsFooterVisible = false,
            };
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not build the progress dialog");
            return null;
        }
    }

    /// <summary>Shows the progress dialog, keeping a failure of it out of the run.</summary>
    private async Task ShowProgressAsync(ContentDialog dialog)
    {
        try
        {
            await contentDialogService.ShowAsync(dialog, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not show the progress dialog");
        }
    }

    private void ShowFailure(string title, string message)
    {
        snackbarService.Show(
            title,
            message,
            ControlAppearance.Danger,
            new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
            TimeSpan.FromSeconds(5)
        );
    }
}
