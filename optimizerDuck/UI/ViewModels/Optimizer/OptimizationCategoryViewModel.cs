using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Configuration;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Revert;
using optimizerDuck.Domain.UI;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services;
using optimizerDuck.Services.Managers;
using optimizerDuck.UI.Dialogs;
using optimizerDuck.UI.ViewModels.Dialogs;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace optimizerDuck.UI.ViewModels.Optimizer;

public partial class OptimizationCategoryViewModel : ViewModel
{
    private static readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly ConcurrentDictionary<
        string,
        Lazy<Task<(string Content, DateTime FetchedAt)>>
    > _sourceCache = new();
    private static readonly TimeSpan SourceCacheTtl = TimeSpan.FromMinutes(5);

    private readonly List<IOptimization> _allOptimizations = [];

    private readonly IOptionsMonitor<AppSettings> _appOptionsMonitor;
    private readonly IOptimizationCategory _category;
    private readonly IContentDialogService _contentDialogService;
    private readonly ILogger<OptimizationCategoryViewModel> _logger;
    private readonly OptimizationService _optimizationService;
    private readonly RevertManager _revertManager;
    private readonly ISnackbarService _snackbarService;

    [ObservableProperty]
    private bool _hideApplied;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleOptimizationCommand))]
    private bool _isProcessing;

    [ObservableProperty]
    private ObservableCollection<IOptimization> _optimizations = [];

    // Search, Filter, Sort
    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _selectedRiskFilterIndex; // 0=All, 1=Safe, 2=Moderate, 3=Risky

    [ObservableProperty]
    private int _selectedSortByIndex; // 0=Risk & Status, 1=Name, 2=Risk, 3=Status

    public OptimizationCategoryViewModel(
        IOptimizationCategory category,
        OptimizationService optimizationService,
        RevertManager revertManager,
        ISnackbarService snackbarService,
        IContentDialogService contentDialogService,
        ILogger<OptimizationCategoryViewModel> logger,
        IOptionsMonitor<AppSettings> appOptionsMonitor
    )
    {
        _category = category;
        _optimizationService = optimizationService;
        _revertManager = revertManager;
        _snackbarService = snackbarService;
        _logger = logger;
        _contentDialogService = contentDialogService;
        _appOptionsMonitor = appOptionsMonitor;
    }

    public bool HasAppliedOptimizations => _allOptimizations.Any(o => o.State.IsApplied);

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedRiskFilterIndexChanged(int value)
    {
        ApplyFilter();
    }

    partial void OnSelectedSortByIndexChanged(int value)
    {
        ApplyFilter();
    }

    partial void OnHideAppliedChanged(bool value)
    {
        ApplyFilter();
    }

    public override async Task OnNavigatedToAsync()
    {
        await LoadOptimizationStatesAsync();
    }

    #region CanExecutes

    private bool CanToggleOptimization(IOptimization optimization)
    {
        return !IsProcessing;
    }

    #endregion CanExecutes

    #region Commands

    /// <summary>
    ///     Toggles the optimization.
    /// </summary>
    /// <param name="optimization">The optimization to toggle.</param>
    [RelayCommand(CanExecute = nameof(CanToggleOptimization))]
    private async Task ToggleOptimizationAsync(IOptimization optimization)
    {
        try
        {
            // Keep a stable reference to the previous state in case we need to roll back UI changes.
            var wasApplied = await RevertManager.IsAppliedAsync(optimization.Id);

            var restorePointCreated = false;
            if (!_optimizationService.WasRequestedRestorePoint)
            {
                var (proceed, created) = await HandleRestorePointAsync();
                if (!proceed)
                {
                    optimization.State.IsApplied = wasApplied;
                    return;
                }

                restorePointCreated = created;
                _optimizationService.WasRequestedRestorePoint = true;
            }

            try
            {
                // Apply optimization if not already applied
                if (!wasApplied)
                {
                    _logger.LogInformation(
                        "===== START applying optimization {OptimizationName} ({OptimizationId}) =====",
                        optimization.OptimizationKey,
                        optimization.Id
                    );
                    var applyResult = await RunWithProcessingDialogAsync(
                        optimization,
                        progress => _optimizationService.ApplyAsync(optimization, progress)
                    );

                    // If result status is completely Failed, we can't retry it
                    if (applyResult.Status == OptimizationSuccessResult.Failed)
                    {
                        ShowOperationOutcomeSnackbar(
                            OperationNotificationState.Failed,
                            OptimizationOperation.Apply,
                            applyResult.Message,
                            restorePointCreated
                        );

                        _logger.LogWarning(
                            "Apply failed {Name} optimization: {Message}",
                            optimization.OptimizationKey,
                            applyResult.Message
                        );

                        _logger.LogInformation(
                            "===== END applying optimization {OptimizationName} ({OptimizationId}) =====",
                            optimization.OptimizationKey,
                            optimization.Id
                        );

                        await OptimizationService.UpdateOptimizationStateAsync(optimization);
                        OnPropertyChanged(nameof(HasAppliedOptimizations));
                        return;
                    }

                    ((App)Application.Current).HasPendingChanges = true;

                    // Retry only if there are some successful steps and some failed steps
                    var retryOutcome = await HandleRetryableFailuresAsync(
                        optimization,
                        applyResult.FailedSteps,
                        OptimizationOperation.Apply
                    );

                    // Explicitly update state after all operations complete
                    await OptimizationService.UpdateOptimizationStateAsync(optimization);

                    // Ensure UI reflects the correct state
                    OnPropertyChanged(nameof(HasAppliedOptimizations));

                    var notificationState = ResolveApplyNotificationState(
                        applyResult,
                        retryOutcome
                    );

                    ShowOperationOutcomeSnackbar(
                        notificationState,
                        OptimizationOperation.Apply,
                        applyResult.Message,
                        restorePointCreated
                    );

                    if (notificationState == OperationNotificationState.Success)
                        _logger.LogInformation(
                            "Successfully applied {Name}",
                            optimization.OptimizationKey
                        );
                    else if (notificationState == OperationNotificationState.Partial)
                        _logger.LogWarning(
                            "Partially applied {Name}",
                            optimization.OptimizationKey
                        );
                    else
                        _logger.LogWarning("Failed to apply {Name}", optimization.OptimizationKey);

                    _logger.LogInformation(
                        "===== END applying optimization {OptimizationName} ({OptimizationId}) =====",
                        optimization.OptimizationKey,
                        optimization.Id
                    );
                }
                else
                {
                    // Revert optimization if already applied
                    _logger.LogInformation(
                        "===== START reverting optimization {OptimizationName} ({OptimizationId}) =====",
                        optimization.OptimizationKey,
                        optimization.Id
                    );

                    var revertResult = await RunWithProcessingDialogAsync(
                        optimization,
                        progress => _optimizationService.RevertAsync(optimization, progress)
                    );

                    ((App)Application.Current).HasPendingChanges = true;

                    var retryOutcome = await HandleRetryableFailuresAsync(
                        optimization,
                        revertResult.FailedSteps,
                        OptimizationOperation.Revert
                    );

                    // Explicitly update state after revert completes
                    await OptimizationService.UpdateOptimizationStateAsync(optimization);

                    // Ensure UI reflects the correct state
                    OnPropertyChanged(nameof(HasAppliedOptimizations));

                    var notificationState = ResolveRevertNotificationState(
                        revertResult,
                        retryOutcome
                    );

                    ShowOperationOutcomeSnackbar(
                        notificationState,
                        OptimizationOperation.Revert,
                        revertResult.Message,
                        restorePointCreated
                    );

                    if (notificationState == OperationNotificationState.Success)
                        _logger.LogInformation(
                            "Successfully reverted {Name}",
                            optimization.OptimizationKey
                        );
                    else if (notificationState == OperationNotificationState.Partial)
                        _logger.LogWarning(
                            "Partially reverted {Name}",
                            optimization.OptimizationKey
                        );
                    else
                        _logger.LogWarning("Failed to revert {Name}", optimization.OptimizationKey);
                    _logger.LogInformation(
                        "===== END reverting optimization {OptimizationName} ({OptimizationId}) =====",
                        optimization.OptimizationKey,
                        optimization.Id
                    );
                }
            }
            catch (Exception ex)
            {
                optimization.State.IsApplied = wasApplied; // revert UI state on failure
                _logger.LogError(
                    ex,
                    "Failed to toggle optimization {Name}",
                    optimization.OptimizationKey
                );
                _snackbarService.Show(
                    Translations.Optimization_Toggle_Snackbar_Error_Title,
                    string.Format(
                        Translations.Optimization_Toggle_Snackbar_Error_Message,
                        ex.Message
                    ),
                    ControlAppearance.Danger,
                    new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                    TimeSpan.FromSeconds(5)
                );
            }
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>
    ///     Shows the details of an optimization.
    /// </summary>
    /// <param name="optimization">The optimization to show details for.</param>
    [RelayCommand]
    private async Task ShowDetailsAsync(IOptimization optimization)
    {
        var dialogViewModel = new OptimizationDetailsViewModel(
            optimization,
            _snackbarService,
            _logger
        );
        var dialogContent = new OptimizationDetailsDialog { DataContext = dialogViewModel };
        var dialog = new ContentDialog
        {
            Title = BuildDialogTitle(optimization),
            Content = dialogContent,
            CloseButtonText = Translations.Button_Ok,
        };
        var result = await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
    }

    [RelayCommand]
    private async Task ViewSourceOnGitHubAsync(IOptimization optimization)
    {
        if (optimization is not BaseOptimization baseOpt || baseOpt.OwnerType == null)
            return;

        var fileName = baseOpt.OwnerType.Name;
        var className = optimization.OptimizationKey;
        var namespacePath = (baseOpt.OwnerType.Namespace ?? string.Empty).Replace('.', '/');
        var relativePath = $"{namespacePath}/{fileName}.cs";
        var url = $"{Shared.GitHubRepoURL}/blob/master/{relativePath}";

        // Fetch source from GitHub raw content to find the class line number
        try
        {
            var rawUrl =
                $"https://raw.githubusercontent.com/itsfatduck/optimizerDuck/master/{relativePath}";

            var cached = _sourceCache.GetOrAdd(rawUrl, CreateSourceCacheEntry);

            var cachedSource = await cached.Value;
            if (DateTime.UtcNow - cachedSource.FetchedAt >= SourceCacheTtl)
            {
                var refreshed = CreateSourceCacheEntry(rawUrl);
                _sourceCache[rawUrl] = refreshed;
                cachedSource = await refreshed.Value;
            }

            var source = cachedSource.Content;

            var lines = source.Split('\n');
            for (var i = 0; i < lines.Length; i++)
                if (
                    lines[i]
                        .Contains(
                            $"class {className} : {nameof(BaseOptimization)}",
                            StringComparison.OrdinalIgnoreCase
                        )
                )
                {
                    url += $"#L{i + 1}";
                    break;
                }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not fetch source to find line number for {Class}",
                className
            );
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open GitHub URL: {Url}", url);
            _snackbarService.Show(
                Translations.Snackbar_OpenLinkFailed_Title,
                Translations.Snackbar_OpenLinkFailed_Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
        }
    }

    #endregion Commands

    #region Helpers

    private async Task LoadOptimizationStatesAsync()
    {
        if (_allOptimizations.Count > 0)
            return;

        IsLoading = true;
        try
        {
            foreach (var optimization in _category.Optimizations)
            {
                optimization.State.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(OptimizationState.IsApplied))
                        OnPropertyChanged(nameof(HasAppliedOptimizations));
                };
                _allOptimizations.Add(optimization);
            }

            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static Lazy<Task<(string Content, DateTime FetchedAt)>> CreateSourceCacheEntry(
        string rawUrl
    )
    {
        return new Lazy<Task<(string Content, DateTime FetchedAt)>>(async () =>
            (await httpClient.GetStringAsync(rawUrl), DateTime.UtcNow)
        );
    }

    /// <summary>
    ///     Apply current filters to the optimizations.
    /// </summary>
    private void ApplyFilter()
    {
        var query = _allOptimizations.AsEnumerable();

        // Search
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            query = query.Where(o =>
                o.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || o.ShortDescription.Contains(search, StringComparison.OrdinalIgnoreCase)
            );
        }

        // Filter by risk
        query = SelectedRiskFilterIndex switch
        {
            1 => query.Where(o => o.Risk == OptimizationRisk.Safe),
            2 => query.Where(o => o.Risk == OptimizationRisk.Moderate),
            3 => query.Where(o => o.Risk == OptimizationRisk.Risky),
            _ => query,
        };

        // Hide applied
        if (HideApplied)
            query = query.Where(o => !o.State.IsApplied);

        // Sort
        query = SelectedSortByIndex switch
        {
            1 => query.OrderBy(o => o.Name),
            2 => query.OrderBy(o => o.Risk),
            3 => query.OrderByDescending(o => o.State.IsApplied ? 1 : 0), // Status
            _ => query.OrderBy(o => o.Risk).ThenByDescending(o => o.State.IsApplied ? 1 : 0), // Risk & Status (default)
        };

        var filtered = query.ToList();
        Optimizations = new ObservableCollection<IOptimization>(filtered);

        OnPropertyChanged(nameof(HasAppliedOptimizations));
    }

    /// <summary>
    ///     Run a long-running action with a processing dialog.
    /// </summary>
    /// <param name="optimization">The optimization to run the action for.</param>
    /// <param name="action">The action to run.</param>
    /// <typeparam name="T">The return type of the action.</typeparam>
    /// <returns>The result of the action.</returns>
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
    /// <param name="optimization">The optimization that failed.</param>
    /// <param name="failedSteps">The failed steps.</param>
    /// <param name="operation">The operation currently being processed.</param>
    /// <returns>The retry outcome.</returns>
    private async Task<FailureResolutionOutcome> HandleRetryableFailuresAsync(
        IOptimization optimization,
        IReadOnlyList<OperationStepResult> failedSteps,
        OptimizationOperation operation
    )
    {
        if (failedSteps.Count == 0)
            return FailureResolutionOutcome.NoFailures;

        // Always keep steps ordered by their original index for a stable UI.
        var remainingFailedSteps = failedSteps.OrderBy(s => s.Index).ToList();
        while (remainingFailedSteps.Count > 0)
        {
            var dialogViewModel = new OptimizationResultDialogViewModel(remainingFailedSteps);
            var dialogContent = new OptimizationResultDialog { DataContext = dialogViewModel };

            var dialog = new ContentDialog
            {
                Title = BuildDialogTitle(optimization),
                Content = dialogContent,
                PrimaryButtonText = Translations.Button_Retry,
                CloseButtonText = Translations.Button_Cancel,
            };

            var result = await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
            if (result != ContentDialogResult.Primary)
                return FailureResolutionOutcome.Deferred;

            if (operation == OptimizationOperation.Revert)
            {
                remainingFailedSteps = (
                    await RunWithProcessingDialogAsync(
                        optimization,
                        progress =>
                            OptimizationService.RetryFailedStepsAsync(
                                remainingFailedSteps,
                                true,
                                _logger,
                                revertManager: null,
                                optimizationId: null,
                                optimizationKey: null,
                                progress: progress
                            )
                    )
                )
                    .OrderBy(s => s.Index)
                    .ToList();

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
    /// <param name="notificationState">The notification state to show.</param>
    /// <param name="operation">The type of operation.</param>
    /// <param name="message">The message to show.</param>
    /// <param name="restorePointCreated">Whether a restore point was created.</param>
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
                + string.Format(
                    Translations.RestorePoint_Snackbar_Success_Message,
                    Shared.RestorePointName
                );

        if (notificationState == OperationNotificationState.Success)
        {
            if (showSuccess)
                _snackbarService.Show(
                    operation == OptimizationOperation.Apply
                        ? Translations.Optimization_Apply_Snackbar_Success_Title
                        : Translations.Optimization_Revert_Snackbar_Success_Title,
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
                    ? Translations.Optimization_Apply_Snackbar_Error_Title
                    : Translations.Optimization_Revert_Snackbar_Error_Title,
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
                    ? Translations.Optimization_Apply_Snackbar_Error_Title
                    : Translations.Optimization_Revert_Snackbar_Error_Title,
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
    /// <returns>Whether the user accepted the restore point dialog.</returns>
    private async Task<(bool Proceed, bool RestorePointCreated)> HandleRestorePointAsync()
    {
        var dialogContent = new RestorePointDialog();
        var dialog = new ContentDialog
        {
            Title = Translations.RestorePoint_Title,
            Content = dialogContent,

            PrimaryButtonText = Translations.Button_Ok,
            SecondaryButtonText = Translations.Button_Skip,
            CloseButtonText = Translations.Button_Cancel,
        };

        var result = await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
        if (result == ContentDialogResult.None)
        { // User cancelled
            _logger.LogInformation("User cancelled the restore point dialog");
            return (false, false);
        }

        if (result == ContentDialogResult.Secondary)
        { // User skipped
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
                    // if setting show snackbar after applied successfully was off, so show it and applied snackbar wont conflict this
                    if (!_appOptionsMonitor.CurrentValue.Optimize.ShowCompletionNotification)
                    {
                        _snackbarService.Show(
                            Translations.RestorePoint_Snackbar_Success_Title,
                            string.Format(
                                Translations.RestorePoint_Snackbar_Success_Message,
                                Shared.RestorePointName
                            ),
                            ControlAppearance.Success,
                            new SymbolIcon
                            {
                                Symbol = SymbolRegular.CheckmarkCircle24,
                                Filled = true,
                            },
                            TimeSpan.FromSeconds(5)
                        );
                        break;
                    }
                    _logger.LogInformation("Successfully created restore point");

                    return (true, true);

                case RestorePointResult.FrequencyLimitReached:
                    _snackbarService.Show(
                        Translations.RestorePoint_Snackbar_Error_Title,
                        Translations.RestorePoint_Snackbar_Warning_LimitReached,
                        ControlAppearance.Caution,
                        new SymbolIcon { Symbol = SymbolRegular.Warning24, Filled = true },
                        TimeSpan.FromSeconds(5)
                    );
                    _logger.LogWarning("Restore point creation frequency limit reached");
                    break;

                case RestorePointResult.Failed:
                default:
                    _snackbarService.Show(
                        Translations.RestorePoint_Snackbar_Error_Title,
                        Translations.RestorePoint_Snackbar_Error_Message,
                        ControlAppearance.Danger,
                        new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                        TimeSpan.FromSeconds(5)
                    );
                    _logger.LogError("Failed to create restore point");
                    break;
            }

            var failedMessage =
                resultState == RestorePointResult.FrequencyLimitReached
                    ? Translations.RestorePoint_Snackbar_Warning_LimitReached
                    : "";
            var failedResult = await _contentDialogService.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions
                {
                    Title = Translations.RestorePoint_Snackbar_Error_Title,
                    Content =
                        failedMessage + $"\n{Translations.RestorePoint_Snackbar_Error_Message}",

                    PrimaryButtonText = Translations.Button_Skip,
                    CloseButtonText = Translations.Button_Cancel,
                },
                CancellationToken.None
            );

            if (failedResult == ContentDialogResult.Primary) // User chose to skip after failure
                return (true, false);

            return (false, false);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to create restore point");
            _snackbarService.Show(
                Translations.RestorePoint_Snackbar_Error_Title,
                Translations.RestorePoint_Snackbar_Error_Message,
                ControlAppearance.Caution,
                new SymbolIcon { Symbol = SymbolRegular.Warning24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
        }

        return (true, false);
    }

    /// <summary>
    ///     Builds the dialog title for an optimization.
    /// </summary>
    /// <param name="optimization">The optimization to build the title for.</param>
    /// <returns>The dialog title.</returns>
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
    }

    private enum OptimizationOperation
    {
        Apply,
        Revert,
    }

    #endregion Helpers
}
