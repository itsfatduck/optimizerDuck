using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Models.ScheduledTask;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.OptimizationServices;
using optimizerDuck.UI.Views.Dialogs;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class ScheduledTasksViewModel : ViewModel
{
    private readonly List<ScheduledTaskModel> _allTasks = [];
    private readonly IContentDialogService _contentDialogService;
    private readonly ILogger<ScheduledTasksViewModel> _logger;
    private readonly ISnackbarService _snackbarService;
    [ObservableProperty] private bool _hideMicrosoftTasks = true;
    private bool _isInitialized;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNotLoading))]
    private bool _isLoading;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _sortByIndex;

    public ScheduledTasksViewModel(
        IContentDialogService contentDialogService,
        ISnackbarService snackbarService,
        ILogger<ScheduledTasksViewModel> logger)
    {
        _contentDialogService = contentDialogService;
        _snackbarService = snackbarService;
        _logger = logger;
    }

    public ObservableCollection<ScheduledTaskModel> Tasks { get; } = [];

    public bool IsNotLoading => !IsLoading;
    public bool HasData => _allTasks.Count > 0;

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
        if (task == null) return;
        try
        {
            await Task.Run(() =>
            {
                if (task.IsEnabled)
                {
                    ScheduledTaskService.EnableTask(task.FullPath);
                    _logger.LogInformation("Enabled task {Name} ({Path})", task.Name, task.FullPath);
                }
                else
                {
                    ScheduledTaskService.DisableTask(task.FullPath);
                    _logger.LogInformation("Disabled task {Name} ({Path})", task.Name, task.FullPath);
                }
            });

            _snackbarService.Show(
                task.IsEnabled
                    ? Translations.ScheduledTasks_Snackbar_Enabled_Title
                    : Translations.ScheduledTasks_Snackbar_Disabled_Title,
                string.Format(Translations.ScheduledTasks_Snackbar_Toggle_Message, task.Name),
                ControlAppearance.Success,
                new SymbolIcon { Symbol = SymbolRegular.CheckmarkCircle24, Filled = true },
                TimeSpan.FromSeconds(3));

            await RefreshTaskState(task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle task {Name} ({Path})", task.Name, task.FullPath);

            // Revert UI without triggering PropertyChanged to avoid infinite loop
            task.PropertyChanged -= Task_PropertyChanged;
            task.IsEnabled = !task.IsEnabled;
            task.PropertyChanged += Task_PropertyChanged;

            _snackbarService.Show(
                Translations.ScheduledTasks_Snackbar_Error_Title,
                ex.Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5));
        }
    }

    [RelayCommand]
    private async Task RunTask(ScheduledTaskModel? task)
    {
        if (task == null) return;
        try
        {
            await Task.Run(() => ScheduledTaskService.RunTask(task.FullPath));
            _logger.LogInformation("Ran task {Name} ({Path})", task.Name, task.FullPath);

            _snackbarService.Show(
                Translations.ScheduledTasks_Snackbar_Run_Title,
                string.Format(Translations.ScheduledTasks_Snackbar_Run_Message, task.Name),
                ControlAppearance.Success,
                new SymbolIcon { Symbol = SymbolRegular.Play24, Filled = true },
                TimeSpan.FromSeconds(3));

            await RefreshTaskState(task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run task {Name} ({Path})", task.Name, task.FullPath);

            _snackbarService.Show(
                Translations.ScheduledTasks_Snackbar_Error_Title,
                ex.Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5));
        }
    }

    [RelayCommand]
    private async Task StopTask(ScheduledTaskModel? task)
    {
        if (task == null) return;
        try
        {
            await Task.Run(() => ScheduledTaskService.StopTask(task.FullPath));
            _logger.LogInformation("Stopped task {Name} ({Path})", task.Name, task.FullPath);

            _snackbarService.Show(
                Translations.ScheduledTasks_Snackbar_Stop_Title,
                string.Format(Translations.ScheduledTasks_Snackbar_Stop_Message, task.Name),
                ControlAppearance.Success,
                new SymbolIcon { Symbol = SymbolRegular.Stop24, Filled = true },
                TimeSpan.FromSeconds(3));

            await RefreshTaskState(task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop task {Name} ({Path})", task.Name, task.FullPath);

            _snackbarService.Show(
                Translations.ScheduledTasks_Snackbar_Error_Title,
                ex.Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5));
        }
    }

    [RelayCommand]
    private async Task DeleteTask(ScheduledTaskModel? task)
    {
        if (task == null) return;

        var dialog = new ContentDialog
        {
            Title = Translations.ScheduledTasks_Dialog_DeleteTitle,
            Content = string.Format(Translations.ScheduledTasks_Dialog_DeleteMessage, task.Name),
            PrimaryButtonText = Translations.Common_Delete,
            CloseButtonText = Translations.Common_Cancel
        };

        var result = await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
        if (result != ContentDialogResult.Primary) return;

        try
        {
            await Task.Run(() => ScheduledTaskService.DeleteTask(task.FullPath));
            _logger.LogInformation("Deleted task {Name} ({Path})", task.Name, task.FullPath);
            _allTasks.RemoveAll(t => t.FullPath == task.FullPath);
            ApplyFilter();

            _snackbarService.Show(
                Translations.ScheduledTasks_Snackbar_Delete_Title,
                string.Format(Translations.ScheduledTasks_Snackbar_Delete_Message, task.Name),
                ControlAppearance.Success,
                new SymbolIcon { Symbol = SymbolRegular.Delete24, Filled = true },
                TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete task {Name} ({Path})", task.Name, task.FullPath);

            _snackbarService.Show(
                Translations.ScheduledTasks_Snackbar_Error_Title,
                ex.Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5));
        }
    }

    [RelayCommand]
    private async Task ViewDetails(ScheduledTaskModel? task)
    {
        if (task == null) return;

        var dialogContent = new ScheduledTaskDetailsDialog { TaskModel = task };
        var dialog = new ContentDialog
        {
            Title = task.Name,
            Content = dialogContent,
            CloseButtonText = Translations.Button_Ok
        };

        await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
    }

    // Filter triggers
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
    ///     Refreshes the state of a single task in-place by re-querying the task scheduler.
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

    private void ApplyFilter()
    {
        // Unsubscribe
        foreach (var task in Tasks) task.PropertyChanged -= Task_PropertyChanged;
        Tasks.Clear();

        var search = SearchText.Trim();
        var hasSearch = !string.IsNullOrWhiteSpace(search);

        var sorted = SortByIndex switch
        {
            0 => _allTasks.OrderBy(t => t.Name),
            1 => _allTasks.OrderBy(t => !t.IsEnabled).ThenBy(t => t.Name),
            2 => _allTasks.OrderBy(t => t.Path).ThenBy(t => t.Name),
            3 => _allTasks.OrderBy(t => t.State).ThenBy(t => t.Name),
            _ => _allTasks.OrderBy(t => t.Name)
        };

        foreach (var task in sorted)
        {
            if (HideMicrosoftTasks && task.IsMicrosoftTask)
                continue;

            if (hasSearch &&
                !task.Name.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !(task.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) &&
                !task.FullPath.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !task.ActionSummary.Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;

            task.PropertyChanged -= Task_PropertyChanged;
            task.PropertyChanged += Task_PropertyChanged;
            Tasks.Add(task);
        }

        OnPropertyChanged(nameof(HasData));
    }

    private async void Task_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScheduledTaskModel.IsEnabled) && sender is ScheduledTaskModel task)
            await ToggleTask(task);
    }
}