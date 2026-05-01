using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services;
using optimizerDuck.UI.Dialogs;
using Wpf.Ui;
using Wpf.Ui.Controls;
using StartupApp = optimizerDuck.Domain.Optimizations.Models.StartupManager.StartupApp;
using StartupTask = optimizerDuck.Domain.Optimizations.Models.StartupManager.StartupTask;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class StartupManagerViewModel : ViewModel
{
    private readonly List<StartupApp> _allApps = [];
    private readonly List<StartupTask> _allTasks = [];
    private readonly IContentDialogService _contentDialogService;
    private readonly ILogger<StartupManagerViewModel> _logger;
    private readonly StartupManagerService _startupManagerService;

    // Per-section: Apps
    [ObservableProperty]
    private string _appSearchText = string.Empty;

    [ObservableProperty]
    private int _appSortByIndex;

    [ObservableProperty]
    private bool _hideMicrosoftTasks = true;
    private bool _isInitialized;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotLoading))]
    [NotifyPropertyChangedFor(nameof(ShowRefreshButton))]
    private bool _isLoading;

    // Per-section: Tasks
    [ObservableProperty]
    private string _taskSearchText = string.Empty;

    [ObservableProperty]
    private int _taskSortByIndex;

    public StartupManagerViewModel(
        StartupManagerService startupManagerService,
        IContentDialogService contentDialogService,
        ILogger<StartupManagerViewModel> logger
    )
    {
        _startupManagerService = startupManagerService;
        _contentDialogService = contentDialogService;
        _logger = logger;
    }

    public ObservableCollection<StartupApp> Apps { get; } = [];
    public ObservableCollection<StartupTask> Tasks { get; } = [];

    public bool IsNotLoading => !IsLoading;

    public bool HasApps => _allApps.Count > 0;
    public bool HasTasks => _allTasks.Count > 0;
    public bool HasData => HasApps || HasTasks;

    public bool HasResults => Apps.Count > 0 || Tasks.Count > 0;
    public bool ShowRefreshButton => IsNotLoading && HasResults;

    public override async Task OnNavigatedToAsync()
    {
        if (_isInitialized)
            return;

        _isInitialized = true;
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadDataAsync();
    }

    [RelayCommand]
    private void OpenStartupSettings()
    {
        try
        {
            Process.Start(
                new ProcessStartInfo("ms-settings:startupapps") { UseShellExecute = true }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Startup Settings");
        }
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
    private void OpenLocation(StartupApp? app)
    {
        if (app?.CanOpenLocation == true)
            try
            {
                Process.Start("explorer.exe", $"/select,\"{app.FilePath}\"");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open location for {Name}", app.Name);
            }
    }

    [RelayCommand]
    private async Task ViewTaskDetails(StartupTask? task)
    {
        if (task == null)
            return;

        var dialog = new ContentDialog
        {
            Title = task.TaskName,
            Content = new StartupTaskDetailsPanel(task),
            CloseButtonText = Translations.Button_Ok,
        };

        await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
    }

    // Apps filter triggers
    partial void OnAppSearchTextChanged(string value)
    {
        ApplyAppFilter();
    }

    partial void OnAppSortByIndexChanged(int value)
    {
        ApplyAppFilter();
    }

    // Tasks filter triggers
    partial void OnTaskSearchTextChanged(string value)
    {
        ApplyTaskFilter();
    }

    partial void OnTaskSortByIndexChanged(int value)
    {
        ApplyTaskFilter();
    }

    partial void OnHideMicrosoftTasksChanged(bool value)
    {
        ApplyTaskFilter();
    }

    private async Task LoadDataAsync()
    {
        IsLoading = true;
        _allApps.Clear();
        _allTasks.Clear();
        Apps.Clear();
        Tasks.Clear();

        OnPropertyChanged(nameof(HasApps));
        OnPropertyChanged(nameof(HasTasks));
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ShowRefreshButton));

        try
        {
            var appsTask = _startupManagerService.GetStartupAppsAsync();
            var tasksTask = _startupManagerService.GetStartupTasksAsync();

            await Task.WhenAll(appsTask, tasksTask);

            _allApps.AddRange(await appsTask);
            _allTasks.AddRange(await tasksTask);

            ApplyAppFilter();
            ApplyTaskFilter();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load startup manager data");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyAppFilter()
    {
        foreach (var app in Apps)
            app.PropertyChanged -= App_PropertyChanged;
        Apps.Clear();

        var search = AppSearchText.Trim();
        var hasSearch = !string.IsNullOrWhiteSpace(search);

        var sortedApps = AppSortByIndex switch
        {
            0 => _allApps.OrderBy(a => a.Name),
            1 => _allApps.OrderBy(a => !a.IsEnabled).ThenBy(a => a.Name),
            2 => _allApps.OrderBy(a => a.LocationDisplay).ThenBy(a => a.Name),
            _ => _allApps.OrderBy(a => a.Name),
        };

        foreach (var app in sortedApps)
        {
            if (
                hasSearch
                && !app.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                && !app.Command.Contains(search, StringComparison.OrdinalIgnoreCase)
                && !app.Publisher.Contains(search, StringComparison.OrdinalIgnoreCase)
            )
                continue;

            app.PropertyChanged -= App_PropertyChanged;
            app.PropertyChanged += App_PropertyChanged;
            Apps.Add(app);
        }

        OnPropertyChanged(nameof(HasApps));
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ShowRefreshButton));
    }

    private void ApplyTaskFilter()
    {
        foreach (var task in Tasks)
            task.PropertyChanged -= Task_PropertyChanged;
        Tasks.Clear();

        var search = TaskSearchText.Trim();
        var hasSearch = !string.IsNullOrWhiteSpace(search);

        var sortedTasks = TaskSortByIndex switch
        {
            0 => _allTasks.OrderBy(t => t.TaskName),
            1 => _allTasks.OrderBy(t => !t.IsEnabled).ThenBy(t => t.TaskName),
            2 => _allTasks.OrderBy(t => t.TaskPath).ThenBy(t => t.TaskName),
            _ => _allTasks.OrderBy(t => t.TaskName),
        };

        foreach (var task in sortedTasks)
        {
            if (HideMicrosoftTasks && task.IsMicrosoftTask)
                continue;

            if (
                hasSearch
                && !task.TaskName.Contains(search, StringComparison.OrdinalIgnoreCase)
                && !(
                    task.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false
                )
                && !task.TaskPath.Contains(search, StringComparison.OrdinalIgnoreCase)
            )
                continue;

            task.PropertyChanged -= Task_PropertyChanged;
            task.PropertyChanged += Task_PropertyChanged;
            Tasks.Add(task);
        }

        OnPropertyChanged(nameof(HasTasks));
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ShowRefreshButton));
    }

    private async void App_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StartupApp.IsEnabled) && sender is StartupApp app)
        {
            try
            {
                await _startupManagerService
                    .ToggleStartupApp(app, app.IsEnabled)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to toggle startup app {Name}", app.Name);
            }
        }
    }

    private async void Task_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StartupTask.IsEnabled) && sender is StartupTask task)
        {
            try
            {
                await _startupManagerService
                    .ToggleStartupTask(task, task.IsEnabled)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to toggle startup task {Name}", task.TaskName);
            }
        }
    }
}
