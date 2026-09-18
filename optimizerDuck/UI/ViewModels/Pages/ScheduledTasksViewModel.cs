using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.ScheduledTask;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.UI.Dialogs;
using Wpf.Ui;
using Wpf.Ui.Controls;
using ScheduledTaskModel = optimizerDuck.Domain.Optimizations.Models.ScheduledTask.ScheduledTaskModel;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class ScheduledTasksViewModel : ViewModel
{
    private readonly List<ScheduledTaskModel> _allTasks = [];
    private readonly IContentDialogService _contentDialogService;
    private readonly ILogger<ScheduledTasksViewModel> _logger;
    private readonly ToolRunPresenter _presenter;
    private readonly ISnackbarService _snackbarService;
    private readonly HashSet<object> _suppressToggle = [];

    [ObservableProperty]
    private bool _hideMicrosoftTasks = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotLoading))]
    [NotifyPropertyChangedFor(nameof(ShowRefreshButton))]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _sortByIndex;

    public ScheduledTasksViewModel(
        ToolRunPresenter presenter,
        IContentDialogService contentDialogService,
        ISnackbarService snackbarService,
        ILogger<ScheduledTasksViewModel> logger
    )
    {
        _presenter = presenter;
        _contentDialogService = contentDialogService;
        _snackbarService = snackbarService;
        _logger = logger;
    }

    public ObservableCollection<ScheduledTaskModel> Tasks { get; } = [];

    public bool IsNotLoading => !IsLoading;
    public bool HasData => _allTasks.Count > 0;

    public bool HasResults => Tasks.Count > 0;
    public bool ShowRefreshButton => IsNotLoading && HasResults;

    /// <inheritdoc />
    public override async Task OnNavigatedToAsync()
    {
        await base.OnNavigatedToAsync();

        if (CrossPageEventBus.HasPendingRefresh<StartupAppsChanged>())
            await LoadDataAsync();
    }

    protected override async Task InitializeOnceAsync()
    {
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadDataAsync();
    }

    [RelayCommand]
    private void OpenTaskScheduler()
    {
        try
        {
            Process.Start(new ProcessStartInfo("taskschd.msc") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Task Scheduler");
        }
    }

    [RelayCommand]
    private async Task ToggleTask(ScheduledTaskModel? task)
    {
        if (task == null)
            return;

        var enable = task.IsEnabled;
        var request = NewToolRequest(context =>
            enable
                ? ScheduledTaskService.EnableTask(context, task.FullPath)
                : ScheduledTaskService.DisableTask(context, task.FullPath)
        );
        var result = await _presenter.RunAsync(request);

        if (!await _presenter.ReportAsync(request, result, FailureTitle))
        {
            await RefreshTaskState(task);
            return;
        }

        _snackbarService.Show(
            enable
                ? Loc.Instance["ScheduledTasks.Snackbar.Enabled.Title"]
                : Loc.Instance["ScheduledTasks.Snackbar.Disabled.Title"],
            Loc.Instance["ScheduledTasks.Snackbar.Toggle.Message", task.Name],
            ControlAppearance.Success,
            new SymbolIcon { Symbol = SymbolRegular.CheckmarkCircle24, Filled = true },
            TimeSpan.FromSeconds(3)
        );

        await RefreshTaskState(task);
    }

    [RelayCommand]
    private async Task RunTask(ScheduledTaskModel? task)
    {
        if (task == null)
            return;

        var request = NewToolRequest(context =>
            ScheduledTaskService.RunTask(context, task.FullPath)
        );
        var result = await _presenter.RunAsync(request);

        if (!await _presenter.ReportAsync(request, result, FailureTitle))
            return;

        _snackbarService.Show(
            Loc.Instance["ScheduledTasks.Snackbar.Run.Title"],
            Loc.Instance["ScheduledTasks.Snackbar.Run.Message", task.Name],
            ControlAppearance.Success,
            new SymbolIcon { Symbol = SymbolRegular.Play24, Filled = true },
            TimeSpan.FromSeconds(3)
        );

        await RefreshTaskState(task);
    }

    [RelayCommand]
    private async Task StopTask(ScheduledTaskModel? task)
    {
        if (task == null)
            return;

        var request = NewToolRequest(context =>
            ScheduledTaskService.StopTask(context, task.FullPath)
        );
        var result = await _presenter.RunAsync(request);

        if (!await _presenter.ReportAsync(request, result, FailureTitle))
            return;

        _snackbarService.Show(
            Loc.Instance["ScheduledTasks.Snackbar.Stop.Title"],
            Loc.Instance["ScheduledTasks.Snackbar.Stop.Message", task.Name],
            ControlAppearance.Success,
            new SymbolIcon { Symbol = SymbolRegular.Stop24, Filled = true },
            TimeSpan.FromSeconds(3)
        );

        await RefreshTaskState(task);
    }

    [RelayCommand]
    private async Task DeleteTask(ScheduledTaskModel? task)
    {
        if (task == null)
            return;

        var dialog = new ContentDialog
        {
            Title = Loc.Instance["ScheduledTasks.Dialog.DeleteTitle"],
            Content = new ScheduledTaskDeleteDialog { DataContext = task },
            PrimaryButtonText = Loc.Instance["Common.Delete"],
            CloseButtonText = Loc.Instance["Common.Cancel"],
        };

        var result = await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
        if (result != ContentDialogResult.Primary)
            return;

        var request = NewToolRequest(context =>
            ScheduledTaskService.DeleteTask(context, task.FullPath)
        );
        var run = await _presenter.RunAsync(request);

        if (!await _presenter.ReportAsync(request, run, FailureTitle))
            return;

        _allTasks.RemoveAll(t => t.FullPath == task.FullPath);
        ApplyFilter();

        _snackbarService.Show(
            Loc.Instance["ScheduledTasks.Snackbar.Delete.Title"],
            Loc.Instance["ScheduledTasks.Snackbar.Delete.Message", task.Name],
            ControlAppearance.Success,
            new SymbolIcon { Symbol = SymbolRegular.Delete24, Filled = true },
            TimeSpan.FromSeconds(3)
        );
    }

    [RelayCommand]
    private async Task ViewDetails(ScheduledTaskModel? task)
    {
        if (task == null)
            return;

        var dialogContent = new ScheduledTaskDetailsDialog { TaskModel = task };
        var dialog = new ContentDialog
        {
            Title = task.Name,
            Content = dialogContent,
            CloseButtonText = Loc.Instance["Button.Ok"],
        };

        await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSortByIndexChanged(int value)
    {
        ApplyFilter();
    }

    partial void OnHideMicrosoftTasksChanged(bool value)
    {
        ApplyFilter();
    }

    /// <summary>
    ///     Re-reads the state and the enabled flag Windows reports for a task, without running it.
    /// </summary>
    private async Task RefreshTaskState(ScheduledTaskModel task)
    {
        try
        {
            var newState = await Task.Run(() => ScheduledTaskService.GetTaskState(task.FullPath));
            if (newState != null)
            {
                task.State = newState;
                task.NotifyStateChanged();
            }

            var enabled = await Task.Run(() =>
                ScheduledTaskService.GetTaskEnabledState(task.FullPath)
            );
            if (enabled is TaskEnabledState.Enabled or TaskEnabledState.Disabled)
            {
                task.PropertyChanged -= Task_PropertyChanged;
                task.IsEnabled = enabled == TaskEnabledState.Enabled;
                task.PropertyChanged += Task_PropertyChanged;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to refresh state for {Name}", task.Name);
        }
    }

    private async Task LoadDataAsync()
    {
        IsLoading = true;
        _allTasks.Clear();
        Tasks.Clear();

        try
        {
            var tasks = await Task.Run(() => ScheduledTaskService.GetAllTasks());
            _allTasks.AddRange(tasks);
            _logger.LogInformation("Loaded {Count} scheduled tasks", _allTasks.Count);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load scheduled tasks");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Builds the run for one tool action.</summary>
    /// <param name="work">The provider call that records the run's steps.</param>
    private OperationRequest NewToolRequest(Func<OptimizationContext, OpResult> work) =>
        new(
            ToolSubjects.ScheduledTasks,
            Loc.Instance["ScheduledTasks.Header.Title"],
            _logger,
            RevertPersistence.Disabled,
            (_, context) =>
                Task.Run(() =>
                {
                    work(context);
                    return context.Changes.ToApplyResult();
                })
        );

    /// <summary>Gets the heading a failed action is reported under.</summary>
    private static string FailureTitle => Loc.Instance["ScheduledTasks.Snackbar.Error.Title"];

    private void ApplyFilter()
    {
        foreach (var task in Tasks)
            task.PropertyChanged -= Task_PropertyChanged;
        Tasks.Clear();

        var search = SearchText.Trim();
        var hasSearch = !string.IsNullOrWhiteSpace(search);

        var sorted = SortByIndex switch
        {
            0 => _allTasks.OrderBy(t => t.Name),
            1 => _allTasks.OrderBy(t => !t.IsEnabled).ThenBy(t => t.Name),
            2 => _allTasks.OrderBy(t => t.Path).ThenBy(t => t.Name),
            3 => _allTasks.OrderBy(t => t.State).ThenBy(t => t.Name),
            _ => _allTasks.OrderBy(t => t.Name),
        };

        foreach (var task in sorted)
        {
            if (HideMicrosoftTasks && task.IsMicrosoftTask)
                continue;

            if (
                hasSearch
                && !task.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                && !(
                    task.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false
                )
                && !task.FullPath.Contains(search, StringComparison.OrdinalIgnoreCase)
                && !task.ActionSummary.Contains(search, StringComparison.OrdinalIgnoreCase)
            )
                continue;

            task.PropertyChanged -= Task_PropertyChanged;
            task.PropertyChanged += Task_PropertyChanged;
            Tasks.Add(task);
        }

        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ShowRefreshButton));
    }

    private async void Task_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (
            e.PropertyName != nameof(ScheduledTaskModel.IsEnabled)
            || sender is not ScheduledTaskModel task
        )
            return;

        // Ignore a toggle that races the run already under way for this task.
        if (!_suppressToggle.Add(task))
            return;

        try
        {
            await ToggleTask(task);
            CrossPageEventBus.NotifyDataChanged<ScheduledTasksChanged>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle scheduled task {Name}", task.Name);
        }
        finally
        {
            _suppressToggle.Remove(task);
        }
    }
}
