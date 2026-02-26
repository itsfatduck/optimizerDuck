using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Models.StartupManager;
using optimizerDuck.Services;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class StartupManagerViewModel : ViewModel
{
    private readonly List<StartupApp> _allApps = [];
    private readonly List<StartupTask> _allTasks = [];
    private readonly ILogger<StartupManagerViewModel> _logger;
    private readonly StartupManagerService _startupManagerService;
    private bool _isInitialized;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNotLoading))]
    private bool _isLoading;

    [ObservableProperty] private string _searchText = string.Empty;

    public StartupManagerViewModel(StartupManagerService startupManagerService, ILogger<StartupManagerViewModel> logger)
    {
        _startupManagerService = startupManagerService;
        _logger = logger;
    }

    public ObservableCollection<StartupApp> Apps { get; } = [];
    public ObservableCollection<StartupTask> Tasks { get; } = [];

    public bool IsNotLoading => !IsLoading;

    public bool HasApps => Apps.Count > 0;
    public bool HasTasks => Tasks.Count > 0;
    public bool HasData => HasApps || HasTasks;

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

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    private async Task LoadDataAsync()
    {
        IsLoading = true;
        _allApps.Clear();
        _allTasks.Clear();
        Apps.Clear();
        Tasks.Clear();

        try
        {
            var apps = await _startupManagerService.GetStartupAppsAsync();
            _allApps.AddRange(apps);

            var tasks = await _startupManagerService.GetStartupTasksAsync();
            _allTasks.AddRange(tasks);

            ApplyFilter();
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

    private void ApplyFilter()
    {
        // Unsubscribe existing items
        foreach (var app in Apps) app.PropertyChanged -= App_PropertyChanged;
        foreach (var task in Tasks) task.PropertyChanged -= Task_PropertyChanged;

        Apps.Clear();
        Tasks.Clear();

        var search = SearchText.Trim();
        var hasSearch = !string.IsNullOrWhiteSpace(search);

        foreach (var app in _allApps)
        {
            if (hasSearch &&
                !app.Name.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !app.Command.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !app.Publisher.Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;

            app.PropertyChanged -= App_PropertyChanged;
            app.PropertyChanged += App_PropertyChanged;
            Apps.Add(app);
        }

        foreach (var task in _allTasks)
        {
            if (hasSearch &&
                !task.TaskName.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !(task.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) &&
                !task.TaskPath.Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;

            task.PropertyChanged -= Task_PropertyChanged;
            task.PropertyChanged += Task_PropertyChanged;
            Tasks.Add(task);
        }

        OnPropertyChanged(nameof(HasApps));
        OnPropertyChanged(nameof(HasTasks));
        OnPropertyChanged(nameof(HasData));
    }

    private async void App_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StartupApp.IsEnabled) && sender is StartupApp app)
            await _startupManagerService.ToggleStartupApp(app, app.IsEnabled);
    }

    private async void Task_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StartupTask.IsEnabled) && sender is StartupTask task)
            await _startupManagerService.ToggleStartupTask(task, task.IsEnabled);
    }
}