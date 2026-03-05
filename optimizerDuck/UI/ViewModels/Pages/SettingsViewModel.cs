using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Models.Config;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services;
using optimizerDuck.Services.Managers;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class SettingsViewModel(
    ConfigManager configManager,
    IOptionsMonitor<AppSettings> appOptionsMonitor,
    OptimizationRegistry optimizationRegistry,
    IContentDialogService contentDialogService,
    ISnackbarService snackbarService,
    ILogger<SettingsViewModel> logger) : ViewModel
{
    [ObservableProperty] private ApplicationTheme _currentApplicationTheme = ApplicationTheme.Unknown;
    private bool _isInitialized;
    [ObservableProperty] private bool _removeProvisioned;

    [ObservableProperty] private string _selectedCultureName = string.Empty;
    [ObservableProperty] private int _shellTimeoutMs;
    public string Version { get; } = Shared.FileVersion;

    //Learn more links
    public string Website { get; } = Shared.WebsiteURL;
    public string Documentation { get; } = Shared.WebsiteURL + "docs/guides/getting-started";
    public string Community { get; } = Shared.CommunityURL;
    public string Contribute { get; } = Shared.ContributeURL;
    public string Acknowledgements { get; } = Shared.AcknowledgementsURL;

    public ObservableCollection<LanguageOption> Languages { get; } =
    [
        new() { DisplayName = "English", Culture = new CultureInfo("en-US") },
        new() { DisplayName = "Tiếng Việt", Culture = new CultureInfo("vi-VN") }
    ];

    public override async Task OnNavigatedToAsync()
    {
        if (!_isInitialized)
            await InitializeViewModel();
    }

    private Task InitializeViewModel()
    {
        SelectedCultureName = appOptionsMonitor.CurrentValue.App.Language;
        ShellTimeoutMs = appOptionsMonitor.CurrentValue.Optimize.ShellTimeoutMs;
        RemoveProvisioned = appOptionsMonitor.CurrentValue.Bloatware.RemoveProvisioned;
        CurrentApplicationTheme = ApplicationThemeManager.GetAppTheme();

        ApplicationThemeManager.Changed += OnThemeChanged;

        _isInitialized = true;
        return Task.CompletedTask;
    }

    private void OnThemeChanged(ApplicationTheme currentApplicationTheme, Color systemAccent)
    {
        // Update the theme if it has been changed elsewhere than in the settings.
        if (CurrentApplicationTheme != currentApplicationTheme) CurrentApplicationTheme = currentApplicationTheme;
    }

    #region Helpers

    private async Task<ContentDialogResult> ConfirmationDialogAsync(string content)
    {
        var dialog = new ContentDialog
        {
            Title = Translations.Dialog_AreYouSure_Title,
            Content = content,
            PrimaryButtonText = Translations.Button_Clear,
            PrimaryButtonAppearance = ControlAppearance.Danger,

            CloseButtonText = Translations.Button_Cancel,

            DefaultButton = ContentDialogButton.Close,
            MaxWidth = 500
        };
        return await contentDialogService.ShowAsync(dialog, CancellationToken.None);
    }

    #endregion Helpers

    #region Commands

    [RelayCommand]
    private void OpenRootDir()
    {
        try
        {
            logger.LogInformation("Opening root directory: {Path}", Shared.RootDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = Shared.RootDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            snackbarService.Show(
                Translations.Snackbar_OpenFailed_Title,
                Translations.Snackbar_OpenFailed_Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
            logger.LogError(ex, "Failed to open root directory: {Path}", Shared.RootDirectory);
        }
    }

    [RelayCommand]
    private async Task ClearResources()
    {
        var result = await ConfirmationDialogAsync(Translations.Settings_ClearResources_Description);
        if (result == ContentDialogResult.Primary)
            OptimizationService.ClearResources(logger);
    }

    [RelayCommand]
    private async Task ClearAllRevertData()
    {
        var result = await ConfirmationDialogAsync(Translations.Settings_ClearRevertData_Description);
        if (result == ContentDialogResult.Primary)
        {
            RevertManager.ClearAllRevertData(logger);
            // Refresh optimizations
            await OptimizationService.UpdateOptimizationStateAsync(
                optimizationRegistry.OptimizationCategories.SelectMany(c => c.Optimizations));
        }
    }

    [RelayCommand]
    private void OpenAcknowledgements()
    {
        try
        {
            logger.LogInformation("Opening acknowledgements page: {Url}", Shared.AcknowledgementsURL);
            Process.Start(new ProcessStartInfo
            {
                FileName = Shared.AcknowledgementsURL,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            snackbarService.Show(
                Translations.Snackbar_OpenLinkFailed_Title,
                Translations.Snackbar_OpenLinkFailed_Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
            logger.LogError(ex, "Failed to open acknowledgements page");
        }
    }

    [RelayCommand]
    private void OpenWebsite(string type)
    {
        try
        {
            switch (type)
            {
                case "Web":
                    logger.LogInformation("Opening page: {Url}", Shared.WebsiteURL);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Shared.WebsiteURL,
                        UseShellExecute = true
                    });
                    break;
                case "Help":
                    logger.LogInformation("Opening page: {Url}", Shared.CommunityURL);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Shared.CommunityURL,
                        UseShellExecute = true
                    });
                    break;
            }
        }
        catch (Exception ex)
        {
            snackbarService.Show(
                Translations.Snackbar_OpenLinkFailed_Title,
                Translations.Snackbar_OpenLinkFailed_Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
            logger.LogError(ex, "Failed to open page");
        }
    }


    [RelayCommand]
    private void ToggleRemoveProvisioned()
    {
        if (!_isInitialized) return;
        _ = configManager.SetAsync("bloatware:removeProvisioned",
            (!appOptionsMonitor.CurrentValue.Bloatware.RemoveProvisioned).ToString());
    }

    [RelayCommand]
    private void OpenLatestRelease()
    {
        try
        {
            logger.LogInformation("Opening latest release page: {Url}", UpdaterService.LatestReleaseUrl);
            Process.Start(new ProcessStartInfo
            {
                FileName = UpdaterService.LatestReleaseUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            snackbarService.Show(
                Translations.Snackbar_OpenLinkFailed_Title,
                Translations.Snackbar_OpenLinkFailed_Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
            logger.LogError(ex, "Failed to open latest release page");
        }
    }

    #endregion Commands

    #region Property Changed

    partial void OnSelectedCultureNameChanged(string value)
    {
        if (!_isInitialized) return;
        if (string.IsNullOrEmpty(value)) return;
        _ = configManager.SetAsync("app:language", value);

        if (value == Loc.CurrentCulture.Name) return;
        contentDialogService.ShowAlertAsync(Translations.Settings_LanguageChanged_Title,
            Translations.Settings_LanguageChanged_Description,
            Translations.Button_Ok, CancellationToken.None);
    }

    partial void OnCurrentApplicationThemeChanged(ApplicationTheme oldValue, ApplicationTheme newValue)
    {
        if (!_isInitialized) return;
        ApplicationThemeManager.Apply(newValue, updateAccent: false);
        _ = configManager.SetAsync("app:theme", newValue.ToString());
    }

    partial void OnShellTimeoutMsChanged(int value)
    {
        if (!_isInitialized) return;
        if (value <= 0) return;
        _ = configManager.SetAsync("optimize:shellTimeoutMs", value.ToString(CultureInfo.InvariantCulture));
    }

    #endregion Property Changed
}