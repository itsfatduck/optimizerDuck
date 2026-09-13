using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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

namespace optimizerDuck.UI.ViewModels.Optimizer;

public partial class OptimizationCategoryViewModel : ViewModel
{
    #region Cache & Constants

    private static readonly TimeSpan FilterDebounceDelay = TimeSpan.FromMilliseconds(250);

    #endregion

    #region Dependencies & Constructor

    private readonly List<IOptimization> _allOptimizations = [];
    private CancellationTokenSource? _filterDebounceCts;

    private readonly IOptionsMonitor<AppSettings> _appOptionsMonitor;
    private readonly IOptimizationCategory _category;
    private readonly IContentDialogService _contentDialogService;
    private readonly ILogger<OptimizationCategoryViewModel> _logger;
    private readonly SystemInfoService _systemInfoService;
    private readonly OptimizationService _optimizationService;
    private readonly RevertManager _revertManager;
    private readonly ISnackbarService _snackbarService;

    public OptimizationCategoryViewModel(
        IOptimizationCategory category,
        OptimizationService optimizationService,
        RevertManager revertManager,
        ISnackbarService snackbarService,
        IContentDialogService contentDialogService,
        SystemInfoService systemInfoService,
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
        _systemInfoService = systemInfoService;
        _appOptionsMonitor = appOptionsMonitor;
    }

    #endregion

    #region Observable Properties

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
    #endregion

    #region Filter & Search

    public bool HasAppliedOptimizations => _allOptimizations.Any(o => o.State.IsApplied);

    partial void OnSearchTextChanged(string value) => ScheduleApplyFilter();

    partial void OnSelectedRiskFilterIndexChanged(int value) => ApplyFilter();

    partial void OnSelectedSortByIndexChanged(int value) => ApplyFilter();

    partial void OnHideAppliedChanged(bool value) => ApplyFilter();

    private void ScheduleApplyFilter()
    {
        _filterDebounceCts?.Cancel();
        _filterDebounceCts?.Dispose();
        _filterDebounceCts = new CancellationTokenSource();
        var token = _filterDebounceCts.Token;

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(FilterDebounceDelay, token).ConfigureAwait(false);
                    if (!token.IsCancellationRequested)
                        await UiThread.InvokeAsync(ApplyFilter, DispatcherPriority.Background);
                }
                catch (OperationCanceledException)
                {
                    // debounced
                }
            },
            token
        );
    }

    #endregion

    #region CanExecutes

    private bool CanToggleOptimization(IOptimization optimization)
    {
        return !IsProcessing;
    }

    #endregion CanExecutes

    #region Commands

    /// <summary>
    ///     Toggles the optimization (apply if not applied, revert if applied).
    /// </summary>
    /// <param name="optimization">The optimization to toggle.</param>
    [RelayCommand(CanExecute = nameof(CanToggleOptimization))]
    private async Task ToggleOptimizationAsync(IOptimization optimization)
    {
        IsProcessing = true;
        try
        {
            var wasApplied = await RevertManager.IsAppliedAsync(optimization.Id);

            var (canProceed, restorePointCreated) = await EnsureRestorePointAsync(
                optimization,
                wasApplied
            );
            if (!canProceed)
                return;

            try
            {
                if (!wasApplied)
                    await ApplyOptimizationAsync(optimization, restorePointCreated);
                else
                    await RevertOptimizationAsync(optimization, restorePointCreated);
            }
            catch (Exception ex)
            {
                optimization.State.IsApplied = wasApplied;
                _logger.LogError(
                    ex,
                    "Failed to toggle optimization {Name}",
                    optimization.OptimizationKey
                );
                _snackbarService.Show(
                    Loc.Instance["Optimization.Toggle.Snackbar.Error.Title"],
                    Loc.Instance["Optimization.Toggle.Snackbar.Error.Message", ex.Message],
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
    ///     Ensures a restore point is created before modifying system settings.
    /// </summary>
    /// <returns>(canProceed, restorePointCreated).</returns>
    private async Task<(bool Proceed, bool RestorePointCreated)> EnsureRestorePointAsync(
        IOptimization optimization,
        bool wasApplied
    )
    {
        if (_optimizationService.WasRequestedRestorePoint)
            return (true, false);

        var (proceed, created) = await HandleRestorePointAsync();
        if (!proceed)
        {
            optimization.State.IsApplied = wasApplied;
            return (false, false);
        }

        _optimizationService.WasRequestedRestorePoint = true;
        return (true, created);
    }

    /// <summary>
    ///     Applies an optimization with progress reporting and retry handling.
    /// </summary>
    /// <param name="optimization">The optimization to apply.</param>
    /// <param name="restorePointCreated">Whether a system restore point was created beforehand.</param>
    private async Task ApplyOptimizationAsync(IOptimization optimization, bool restorePointCreated)
    {
        _logger.LogInformation(
            "===== START applying {Name} ({Id}) =====",
            optimization.OptimizationKey,
            optimization.Id
        );

        var applyResult = await RunWithProcessingDialogAsync(
            optimization,
            p => _optimizationService.ApplyAsync(optimization, p)
        );

        // Complete failure: can't retry
        if (applyResult.Status == OptimizationSuccessResult.Failed)
        {
            ShowOperationOutcomeSnackbar(
                OperationNotificationState.Failed,
                OptimizationOperation.Apply,
                applyResult.Message,
                restorePointCreated
            );
            _logger.LogWarning("Apply failed: {Message}", applyResult.Message);
            await FinalizeOperationAsync(optimization);
            return;
        }

        if (Application.Current is App app)
            app.HasPendingChanges = true;

        var retryOutcome = await HandleRetryableFailuresAsync(
            optimization,
            applyResult.FailedSteps,
            OptimizationOperation.Apply
        );

        var notificationState = ResolveApplyNotificationState(applyResult, retryOutcome);
        await FinalizeOperationAsync(optimization);
        ShowOperationOutcomeSnackbar(
            notificationState,
            OptimizationOperation.Apply,
            applyResult.Message,
            restorePointCreated
        );

        LogOperationOutcome(notificationState, "apply", optimization);
        _logger.LogInformation(
            "===== END applying {Name} ({Id}) =====",
            optimization.OptimizationKey,
            optimization.Id
        );
    }

    /// <summary>
    ///     Reverts an optimization with progress reporting and retry handling.
    /// </summary>
    /// <param name="optimization">The optimization to revert.</param>
    /// <param name="restorePointCreated">Whether a system restore point was created beforehand.</param>
    private async Task RevertOptimizationAsync(IOptimization optimization, bool restorePointCreated)
    {
        _logger.LogInformation(
            "===== START reverting {Name} ({Id}) =====",
            optimization.OptimizationKey,
            optimization.Id
        );

        var revertResult = await RunWithProcessingDialogAsync(
            optimization,
            p => _optimizationService.RevertAsync(optimization, p)
        );

        if (Application.Current is App app)
            app.HasPendingChanges = true;

        var retryOutcome = await HandleRetryableFailuresAsync(
            optimization,
            revertResult.FailedSteps,
            OptimizationOperation.Revert
        );

        var notificationState = ResolveRevertNotificationState(revertResult, retryOutcome);
        await FinalizeOperationAsync(optimization);
        ShowOperationOutcomeSnackbar(
            notificationState,
            OptimizationOperation.Revert,
            revertResult.Message,
            restorePointCreated
        );

        LogOperationOutcome(notificationState, "revert", optimization);
        _logger.LogInformation(
            "===== END reverting {Name} ({Id}) =====",
            optimization.OptimizationKey,
            optimization.Id
        );
    }

    /// <summary>
    ///     Updates optimization state and notifies the UI.
    /// </summary>
    private static async Task FinalizeOperationAsync(IOptimization optimization)
    {
        await OptimizationService.UpdateOptimizationStateAsync(optimization);
    }

    private void LogOperationOutcome(
        OperationNotificationState notificationState,
        string operationName,
        IOptimization optimization
    )
    {
        var level =
            notificationState == OperationNotificationState.Success ? LogLevel.Information
            : notificationState == OperationNotificationState.Partial ? LogLevel.Warning
            : LogLevel.Warning;

        var pastTense = operationName switch
        {
            "apply" => "applied",
            "revert" => "reverted",
            _ => $"{operationName}ed",
        };

        var message =
            notificationState == OperationNotificationState.Success ? $"Successfully {pastTense}"
            : notificationState == OperationNotificationState.Partial ? $"Partially {pastTense}"
            : $"Failed to {operationName}";

        _logger.Log(level, "{Message}: {Name}", message, optimization.OptimizationKey);
    }

    /// <summary>
    ///     Hides the unsupported condition state for this session, returning the
    ///     optimization to its normal card so the user can apply it anyway.
    ///     The hide choice is not persisted.
    /// </summary>
    [RelayCommand]
    private void HideCondition(IOptimization optimization)
    {
        if (optimization is BaseOptimization baseOptimization)
            baseOptimization.IsConditionHidden = true;
    }

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
            CloseButtonText = Loc.Instance["Button.Ok"],
        };
        var result = await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
    }

    [RelayCommand]
    private async Task ViewSourceOnGitHubAsync(IOptimization optimization)
    {
        if (optimization is not BaseOptimization baseOpt || baseOpt.OwnerType == null)
            return;

        await GitHubSourceHelper.OpenSourceOnGitHubAsync(
            baseOpt.OwnerType,
            optimization.OptimizationKey,
            nameof(BaseOptimization),
            _logger,
            _snackbarService
        );
    }

    #endregion Commands

    #region Helpers

    protected override async Task InitializeOnceAsync()
    {
        IsLoading = true;
        try
        {
            foreach (var optimization in _category.Optimizations)
            {
                // Listen to the optimization's own PropertyChanged instead of the nested
                // State instance so a replaced State (BaseOptimization re-raises the
                // change) can never orphan this subscription.
                if (optimization is INotifyPropertyChanged notify)
                    notify.PropertyChanged += OnOptimizationStateChanged;
                _allOptimizations.Add(optimization);
            }

            var snapshot = await _systemInfoService.EnsureSnapshotAsync();
            if (snapshot.IsUnknown)
                _logger.LogWarning(
                    "System snapshot is unavailable; conditions fail open for this session"
                );

            await UiThread.InvokeAsync(() =>
            {
                EvaluateConditions(snapshot);
                ApplyFilter();
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    ///     Re-evaluates every optimization's condition and re-applies the filter when the
    ///     system snapshot is refreshed (e.g. hardware changed) or the UI language changes.
    ///     Marshalled to the UI thread by <see cref="UiThread"/>.
    /// </summary>
    private void OnSnapshotRefreshed(object? sender, SystemInfo snapshot) =>
        ReEvaluateConditions(snapshot);

    private void OnCultureChanged(object? sender, PropertyChangedEventArgs e) =>
        ReEvaluateConditions(_systemInfoService.Snapshot);

    private void ReEvaluateConditions(SystemInfo snapshot)
    {
        _ = UiThread.InvokeAsync(() =>
        {
            EvaluateConditions(snapshot);
            ApplyFilter();
        });
    }

    /// <summary>
    ///     Keeps <see cref="HasAppliedOptimizations"/> in sync when any optimization's
    ///     <see cref="IOptimization.State"/> changes (applied status or instance swap).
    /// </summary>
    private void OnOptimizationStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IOptimization.State))
            OnPropertyChanged(nameof(HasAppliedOptimizations));
    }

    public override Task OnNavigatedToAsync()
    {
        _systemInfoService.SnapshotRefreshed += OnSnapshotRefreshed;
        Loc.Instance.PropertyChanged += OnCultureChanged;
        return base.OnNavigatedToAsync();
    }

    public override Task OnNavigatedFromAsync()
    {
        _systemInfoService.SnapshotRefreshed -= OnSnapshotRefreshed;
        Loc.Instance.PropertyChanged -= OnCultureChanged;
        return base.OnNavigatedFromAsync();
    }

    /// <summary>
    ///     Re-evaluates every optimization's condition against the system snapshot.
    /// </summary>
    private void EvaluateConditions(SystemInfo snapshot)
    {
        ConditionEvaluator.EvaluateAll(
            _allOptimizations,
            o => o.ConditionType,
            (o, r) => o.ConditionResult = r,
            snapshot,
            _logger
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

    #endregion Helpers
}
