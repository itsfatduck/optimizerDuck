using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class DashboardViewModel : ViewModel
{
    private readonly IContentDialogService _contentDialogService;
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly ISnackbarService _snackbarService;
    private readonly SystemInfoService _systemInfoService;
    private readonly UpdaterService _updaterService;

    private readonly DispatcherTimer _updateTimer;
    [ObservableProperty] private ApplicationTheme _currentApplicationTheme = ApplicationTheme.Unknown;

    private bool _isInitialized;

    [ObservableProperty] private bool _isLoading;
    private bool _isUpdateInfoOpen;
    [ObservableProperty] private string? _latestVersion;
    [ObservableProperty] private DiskInfo _runtimeDisk = DiskInfo.Unknown;

    [ObservableProperty] private RamInfo _runtimeRam = RamInfo.Unknown;
    [ObservableProperty] private SystemSnapshot _systemInfo = SystemSnapshot.Unknown;
    private bool _updateNotified;

    public DashboardViewModel(SystemInfoService systemInfoService, ISnackbarService snackbarService,
        ILogger<DashboardViewModel> logger, UpdaterService updaterService, IContentDialogService contentDialogService)
    {
        _systemInfoService = systemInfoService;
        _snackbarService = snackbarService;
        _logger = logger;
        _updaterService = updaterService;
        _contentDialogService = contentDialogService;

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _updateTimer.Tick += async (s, e) => await UpdateRuntimeInfoAsync();

        CurrentApplicationTheme = ApplicationThemeManager.GetAppTheme();
        ApplicationThemeManager.Changed += OnThemeChanged;
    }

    public bool IsUpdateInfoOpen
    {
        get => _isUpdateInfoOpen;
        set
        {
            if (_isUpdateInfoOpen == value)
                return;

            _isUpdateInfoOpen = value;
            OnPropertyChanged();

            if (!_isUpdateInfoOpen && _updateNotified) OpenLatestRelease();
        }
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
            var (result, version) = await _updaterService.CheckForUpdatesAsync();
            if (result)
            {
                _updateNotified = true;
                IsUpdateInfoOpen = true;
                LatestVersion = version;
            }

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

    #endregion Property Changed

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
            _snackbarService.Show(
                Translations.Snackbar_OpenFailed_Title,
                Translations.Snackbar_OpenFailed_Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
            _logger.LogError(ex, "Failed to open {Action} link", action);
        }
    }

    [RelayCommand]
    private void Run(string action)
    {
        try
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
        catch (Exception ex)
        {
            _snackbarService.Show(
                Translations.Snackbar_OpenFailed_Title,
                Translations.Snackbar_OpenFailed_Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
            _logger.LogError(ex, "Failed to run action {Action}", action);
        }
    }

    #endregion Commands

    #region Helpers

    private async Task LoadSystemInfoAsync()
    {
        IsLoading = true;

        try
        {
            var snapshot = await _systemInfoService.RefreshAsync();
            SystemInfo = snapshot;
            RuntimeRam = snapshot.Ram;
            RuntimeDisk = snapshot.Disk;
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
            var diskInfo = await Task.Run(DiskProvider.Get);
            RuntimeRam = ramInfo;
            RuntimeDisk = diskInfo;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update runtime info");
        }
    }

    private void OpenLatestRelease()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = UpdaterService.LatestReleaseUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _snackbarService.Show(
                Translations.Snackbar_OpenLinkFailed_Title,
                Translations.Snackbar_OpenLinkFailed_Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
            _logger.LogError(ex, "Failed to open latest release page");
        }
    }

    #endregion Helpers
}