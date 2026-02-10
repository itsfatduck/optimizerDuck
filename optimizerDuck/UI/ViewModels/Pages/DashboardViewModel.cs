using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Services;
using Wpf.Ui.Appearance;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class DashboardViewModel : ViewModel
{
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly SystemInfoService _systemInfoService;
    private readonly DispatcherTimer _updateTimer;
    [ObservableProperty] private ApplicationTheme _currentApplicationTheme = ApplicationTheme.Unknown;

    private bool _isInitialized;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private RamInfo _runtimeRam = RamInfo.Unknown;
    [ObservableProperty] private SystemSnapshot _systemInfo = SystemSnapshot.Unknown;

    public DashboardViewModel(SystemInfoService systemInfoService, ILogger<DashboardViewModel> logger)
    {
        _systemInfoService = systemInfoService;
        _logger = logger;

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _updateTimer.Tick += async (s, e) => await UpdateRuntimeInfoAsync();
        CurrentApplicationTheme = ApplicationThemeManager.GetAppTheme();
        ApplicationThemeManager.Changed += OnThemeChanged;
    }

    public override async Task OnNavigatedToAsync()
    {
        if (IsLoading) return;

        // Load system information when user navigates to the page
        // This will also update the runtime RAM info
        await LoadSystemInfoAsync();
        if (!_isInitialized)
        {
            _systemInfoService.LogSummary();
            _isInitialized = true;
        }

        // Start the timer to update runtime infos
        _updateTimer.Start();
    }

    public override Task OnNavigatedFromAsync()
    {
        _updateTimer.Stop();
        return base.OnNavigatedFromAsync();
    }


    #region Property Changed

    private void OnThemeChanged(ApplicationTheme currentApplicationTheme, Color systemAccent)
    {
        if (CurrentApplicationTheme != currentApplicationTheme) CurrentApplicationTheme = currentApplicationTheme;
    }

    #endregion

    #region Commands

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadSystemInfoAsync();
    }

    [RelayCommand]
    private void OpenAction(string action)
    {
        try
        {
            switch (action)
            {
                case "Discord":
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Shared.DiscordInviteURL,
                        UseShellExecute = true
                    });
                    break;

                case "GitHub":
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Shared.GitHubRepoURL,
                        UseShellExecute = true
                    });
                    break;

                case "Support":
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Shared.SupportMeURL,
                        UseShellExecute = true
                    });
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open {Action} link", action);
        }
    }

    [RelayCommand]
    private void Run(string action)
    {
        switch (action)
        {
            case "Settings":
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:",
                    UseShellExecute = true
                });
                break;

            case "TaskManager":
                Process.Start(new ProcessStartInfo
                {
                    FileName = "taskmgr",
                    UseShellExecute = true
                });
                break;

            case "ControlPanel":
                Process.Start(new ProcessStartInfo
                {
                    FileName = "control",
                    UseShellExecute = true
                });
                break;
        }
    }

    #endregion

    #region Helpers

    private async Task LoadSystemInfoAsync()
    {
        IsLoading = true;

        try
        {
            var snapshot = await _systemInfoService.RefreshAsync();
            SystemInfo = snapshot;
            RuntimeRam = snapshot.Ram;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load system information");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task UpdateRuntimeInfoAsync()
    {
        try
        {
            var ramInfo = await Task.Run(RamProvider.Get);
            RuntimeRam = ramInfo;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update runtime info");
        }
    }

    #endregion
}