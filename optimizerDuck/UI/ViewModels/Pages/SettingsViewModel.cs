using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Configuration;
using optimizerDuck.Domain.UI;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.Optimization;
using optimizerDuck.Services.Revert;
using optimizerDuck.Services.System;
using optimizerDuck.UI.Behaviors;
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
    ILogger<SettingsViewModel> logger
) : ViewModel
{
    [ObservableProperty]
    private ApplicationTheme _currentApplicationTheme = ApplicationTheme.Unknown;

    [ObservableProperty]
    private bool _removeProvisioned;

    [ObservableProperty]
    private string _selectedCultureName = string.Empty;

    [ObservableProperty]
    private int _shellTimeoutMs;

    [ObservableProperty]
    private bool _showSnackbarNotificationAfterAppliedSuccessfully;

    [ObservableProperty]
    private bool _smoothScrolling;
    public string Version { get; } = Shared.FileVersion;

    public ObservableCollection<LanguageOption> Languages { get; } = new(SupportedLanguages.All);

    protected override Task InitializeOnceAsync()
    {
        SelectedCultureName = appOptionsMonitor.CurrentValue.App.Language;
        ShellTimeoutMs = appOptionsMonitor.CurrentValue.Optimize.ShellTimeoutMs;
        ShowSnackbarNotificationAfterAppliedSuccessfully = appOptionsMonitor
            .CurrentValue
            .Optimize
            .ShowCompletionNotification;
        SmoothScrolling = appOptionsMonitor.CurrentValue.Optimize.SmoothScrolling;
        SmoothScrollBehavior.GlobalEnabled = SmoothScrolling;
        RemoveProvisioned = appOptionsMonitor.CurrentValue.Bloatware.RemoveProvisioned;
        CurrentApplicationTheme = ApplicationThemeManager.GetAppTheme();

        ApplicationThemeManager.Changed += OnThemeChanged;

        return Task.CompletedTask;
    }

    private void OnThemeChanged(ApplicationTheme currentApplicationTheme, Color systemAccent)
    {
        // Update the theme if it has been changed elsewhere than in the settings.
        if (CurrentApplicationTheme != currentApplicationTheme)
            CurrentApplicationTheme = currentApplicationTheme;
    }

    #region Helpers

    private async Task<ContentDialogResult> ConfirmationDialogAsync(string content)
    {
        var dialog = new ContentDialog
        {
            Title = Loc.Instance["Dialog.AreYouSure.Title"],
            Content = content,
            PrimaryButtonText = Loc.Instance["Button.Clear"],
            PrimaryButtonAppearance = ControlAppearance.Danger,

            CloseButtonText = Loc.Instance["Button.Cancel"],

            DefaultButton = ContentDialogButton.Close,
            MaxWidth = 500,
        };
        return await contentDialogService.ShowAsync(dialog, CancellationToken.None);
    }

    #endregion Helpers

    private async Task SaveConfigAsync(Func<Task> saveAction, Func<Task>? revertAction = null)
    {
        try
        {
            await saveAction();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save configuration");
            if (revertAction != null)
            {
                try
                {
                    await revertAction();
                }
                catch (Exception revertEx)
                {
                    logger.LogError(revertEx, "Failed to revert UI property");
                }
            }
        }
    }

    #region Commands

    [RelayCommand]
    private void OpenRootDir()
    {
        try
        {
            logger.LogInformation("Opening root directory: {Path}", Shared.RootDirectory);
            Process.Start(
                new ProcessStartInfo { FileName = Shared.RootDirectory, UseShellExecute = true }
            );
        }
        catch (Exception ex)
        {
            snackbarService.Show(
                Loc.Instance["Snackbar.OpenFailed.Title"],
                Loc.Instance["Snackbar.OpenFailed.Message"],
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
            logger.LogError(ex, "Failed to open root directory: {Path}", Shared.RootDirectory);
        }
    }

    [RelayCommand]
    private async Task ClearDownloads()
    {
        var result = await ConfirmationDialogAsync(
            Loc.Instance["Settings.ClearDownloads.Description"]
        );
        if (result == ContentDialogResult.Primary)
            OptimizationService.ClearDownloads(logger);
    }

    [RelayCommand]
    private async Task ClearAllRevertData()
    {
        var result = await ConfirmationDialogAsync(
            Loc.Instance["Settings.ClearRevertData.Description"]
        );
        if (result == ContentDialogResult.Primary)
        {
            RevertManager.ClearAllRevertData(logger);
            // Refresh optimizations
            await OptimizationService.UpdateOptimizationStateAsync(
                optimizationRegistry.OptimizationCategories.SelectMany(c => c.Optimizations)
            );
        }
    }

    [RelayCommand]
    private void OpenWebsite(string type)
    {
        try
        {
            switch (type)
            {
                case "Documentation":
                    logger.LogInformation(
                        "Opening page: {Url}",
                        Shared.WebsiteURL + "docs/guides/getting-started"
                    );
                    Process.Start(
                        new ProcessStartInfo
                        {
                            FileName = Shared.WebsiteURL + "docs/guides/getting-started",
                            UseShellExecute = true,
                        }
                    );
                    break;

                case "GitHub":
                    logger.LogInformation("Opening page: {Url}", Shared.GitHubRepoURL);
                    Process.Start(
                        new ProcessStartInfo
                        {
                            FileName = Shared.GitHubRepoURL,
                            UseShellExecute = true,
                        }
                    );
                    break;

                case "Acknowledgements":
                    logger.LogInformation("Opening page: {Url}", Shared.AcknowledgementsURL);
                    Process.Start(
                        new ProcessStartInfo
                        {
                            FileName = Shared.AcknowledgementsURL,
                            UseShellExecute = true,
                        }
                    );
                    break;

                case "Help":
                    logger.LogInformation("Opening page: {Url}", Shared.CommunityURL);
                    Process.Start(
                        new ProcessStartInfo
                        {
                            FileName = Shared.CommunityURL,
                            UseShellExecute = true,
                        }
                    );
                    break;
            }
        }
        catch (Exception ex)
        {
            snackbarService.Show(
                Loc.Instance["Snackbar.OpenLinkFailed.Title"],
                Loc.Instance["Snackbar.OpenLinkFailed.Message"],
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
            logger.LogWarning(ex, "Failed to open page");
        }
    }

    [RelayCommand]
    private async Task ToggleRemoveProvisioned()
    {
        if (!IsInitialized)
            return;
        try
        {
            await configManager.SetAsync(
                "bloatware:removeProvisioned",
                (!appOptionsMonitor.CurrentValue.Bloatware.RemoveProvisioned).ToString()
            );
            RemoveProvisioned = appOptionsMonitor.CurrentValue.Bloatware.RemoveProvisioned;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to toggle RemoveProvisioned setting");
            RemoveProvisioned = appOptionsMonitor.CurrentValue.Bloatware.RemoveProvisioned;
        }
    }

    [RelayCommand]
    private async Task ToggleShowCompletionNotification()
    {
        if (!IsInitialized)
            return;
        try
        {
            await configManager.SetAsync(
                "optimize:showCompletionNotification",
                (!appOptionsMonitor.CurrentValue.Optimize.ShowCompletionNotification).ToString()
            );
            ShowSnackbarNotificationAfterAppliedSuccessfully = appOptionsMonitor
                .CurrentValue
                .Optimize
                .ShowCompletionNotification;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to toggle ShowCompletionNotification setting");
            ShowSnackbarNotificationAfterAppliedSuccessfully = appOptionsMonitor
                .CurrentValue
                .Optimize
                .ShowCompletionNotification;
        }
    }

    [RelayCommand]
    private async Task ToggleSmoothScrolling()
    {
        if (!IsInitialized)
            return;
        try
        {
            await configManager.SetAsync(
                "optimize:smoothScrolling",
                (!appOptionsMonitor.CurrentValue.Optimize.SmoothScrolling).ToString()
            );
            SmoothScrolling = appOptionsMonitor.CurrentValue.Optimize.SmoothScrolling;
            SmoothScrollBehavior.GlobalEnabled = SmoothScrolling;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to toggle SmoothScrolling setting");
            SmoothScrolling = appOptionsMonitor.CurrentValue.Optimize.SmoothScrolling;
        }
    }

    [RelayCommand]
    private void OpenLatestRelease()
    {
        try
        {
            logger.LogInformation(
                "Opening latest release page: {Url}",
                UpdaterService.LatestReleaseUrl
            );
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = UpdaterService.LatestReleaseUrl,
                    UseShellExecute = true,
                }
            );
        }
        catch (Exception ex)
        {
            snackbarService.Show(
                Loc.Instance["Snackbar.OpenLinkFailed.Title"],
                Loc.Instance["Snackbar.OpenLinkFailed.Message"],
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
        if (!IsInitialized)
            return;
        if (string.IsNullOrEmpty(value))
            return;

        var oldValue = appOptionsMonitor.CurrentValue.App.Language;
        _ = SafeFireAndForgetAsync(
            async () =>
            {
                await configManager.SetAsync(x => x.App.Language, value);
            },
            async () =>
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    SelectedCultureName = oldValue
                );
            }
        );

        if (value == Loc.CurrentCulture.Name)
            return;

        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            Loc.Instance.ChangeCulture(new CultureInfo(value));
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    partial void OnCurrentApplicationThemeChanged(
        ApplicationTheme oldValue,
        ApplicationTheme newValue
    )
    {
        if (!IsInitialized)
            return;
        ApplicationThemeManager.Apply(newValue, updateAccent: false);

        _ = SaveConfigAsync(
            async () =>
            {
                await configManager.SetAsync(x => x.App.Theme, newValue);
            },
            async () =>
            {
                CurrentApplicationTheme = oldValue;
            }
        );
    }

    partial void OnShellTimeoutMsChanged(int value)
    {
        if (!IsInitialized)
            return;
        if (value <= 0)
            return;

        var oldValue = appOptionsMonitor.CurrentValue.Optimize.ShellTimeoutMs;
        _ = SaveConfigAsync(
            async () =>
            {
                await configManager.SetAsync(x => x.Optimize.ShellTimeoutMs, value);
            },
            async () =>
            {
                ShellTimeoutMs = oldValue;
            }
        );
    }

    #endregion Property Changed

    /// <summary>
    ///     Fires an async task without awaiting it, but catches all exceptions
    /// </summary>
    private async Task SafeFireAndForgetAsync(
        Func<Task> taskFactory,
        Func<Task>? revertAction = null
    )
    {
        try
        {
            await taskFactory();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred in a fire-and-forget operation");
            if (revertAction != null)
            {
                try
                {
                    await revertAction();
                }
                catch (Exception revertEx)
                {
                    logger.LogError(revertEx, "Failed to revert after error");
                }
            }
        }
    }
}
