using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    IContentDialogService contentDialogService) : ViewModel
{
    [ObservableProperty] private ApplicationTheme _currentApplicationTheme = ApplicationTheme.Unknown;
    private bool _isInitialized;
    [ObservableProperty] private string _selectedCultureName = string.Empty;
    [ObservableProperty] private int _shellTimeoutMs;
    public string Version { get; } = Shared.FileVersion;

    //Learn more links
    public string DiscordInvite { get; } = Shared.DiscordInviteURL;

    public string GitHubRepo { get; } = Shared.GitHubRepoURL;
    public string Documentation { get; } = Shared.GitHubRepoURL + "/wiki";
    public string SupportMe { get; } = Shared.SupportMeURL;
    public string Acknowledgements { get; } = Shared.AcknowledgementsURL;

    public ObservableCollection<LanguageOption> Languages { get; } =
    [
        new("English", new CultureInfo("en-US")),
        new("Tiếng Việt", new CultureInfo("vi-VN"))
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
        Process.Start(new ProcessStartInfo
        {
            FileName = Shared.RootDirectory,
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private async Task ClearResources()
    {
        var result = await ConfirmationDialogAsync(Translations.Settings_ClearResources_Description);
        if (result == ContentDialogResult.Primary)
            OptimizationService.ClearResourcesAsync();
    }

    [RelayCommand]
    private async Task ClearAllRevertData()
    {
        var result = await ConfirmationDialogAsync(Translations.Settings_ClearRevertData_Description);
        if (result == ContentDialogResult.Primary)
        {
            RevertManager.ClearAllRevertDataAsync();
            // Refresh optimizations
            await OptimizationService.UpdateOptimizationStateAsync(
                optimizationRegistry.OptimizationCategories.SelectMany(c => c.Optimizations));
        }
    }
    
    [RelayCommand]
    private void OpenAcknowledgements()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = Shared.AcknowledgementsURL,
            UseShellExecute = true
        });
    }

    #endregion Commands

    #region Property Changed

    partial void OnSelectedCultureNameChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        _ = configManager.SetAsync("app:language", value);

        if (value == Loc.CurrentCulture.Name) return;
        contentDialogService.ShowAlertAsync(Translations.Settings_LanguageChanged_Title,
            Translations.Settings_LanguageChanged_Description,
            Translations.Button_Ok, CancellationToken.None);
    }

    partial void OnCurrentApplicationThemeChanged(ApplicationTheme oldValue, ApplicationTheme newValue)
    {
        ApplicationThemeManager.Apply(newValue, updateAccent: false);
        _ = configManager.SetAsync("app:theme", newValue.ToString());
    }

    partial void OnShellTimeoutMsChanged(int value)
    {
        if (value <= 0) return;
        _ = configManager.SetAsync("optimize:shellTimeoutMs", value.ToString(CultureInfo.InvariantCulture));
    }

    #endregion Property Changed
}