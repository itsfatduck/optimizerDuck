using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Configuration;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Configuration;
using Wpf.Ui.Appearance;

namespace optimizerDuck.UI.ViewModels.Dialogs;

public partial class LegalDialogViewModel(
    ConfigManager configManager,
    IOptionsMonitor<AppSettings> appOptionsMonitor,
    ILogger<LegalDialogViewModel> logger
) : ViewModel
{
    [ObservableProperty]
    private ApplicationTheme _currentApplicationTheme = ApplicationTheme.Unknown;

    [ObservableProperty]
    private string _selectedCultureName = string.Empty;

    public ObservableCollection<LanguageOption> Languages { get; } = new(SupportedLanguages.All);

    protected override Task InitializeOnceAsync()
    {
        SelectedCultureName = appOptionsMonitor.CurrentValue.App.Language;
        CurrentApplicationTheme = ApplicationThemeManager.GetAppTheme();
        ApplicationThemeManager.Changed += OnThemeChanged;
        return Task.CompletedTask;
    }

    private void OnThemeChanged(
        ApplicationTheme currentApplicationTheme,
        System.Windows.Media.Color systemAccent
    )
    {
        if (CurrentApplicationTheme != currentApplicationTheme)
            CurrentApplicationTheme = currentApplicationTheme;
    }

    partial void OnSelectedCultureNameChanged(string value)
    {
        if (!IsInitialized || string.IsNullOrEmpty(value))
            return;

        var oldValue = appOptionsMonitor.CurrentValue.App.Language;
        _ = SafeFireAndForgetAsync(
            async () => await configManager.SetAsync(x => x.App.Language, value),
            async () =>
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    SelectedCultureName = oldValue
                )
        );

        if (value == Loc.CurrentCulture.Name)
            return;

        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            Loc.Instance.ChangeCulture(new CultureInfo(value));
            logger.LogInformation("Language changed to {Language} from LegalDialog", value);
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
        _ = SafeFireAndForgetAsync(
            async () => await configManager.SetAsync(x => x.App.Theme, newValue),
            async () =>
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    CurrentApplicationTheme = oldValue
                )
        );
    }

    [RelayCommand]
    private void OpenWebsite(string type)
    {
        try
        {
            var url = type switch
            {
                "GitHub" => Shared.GitHubRepoURL,
                "Documentation" or "Docs" => Shared.WebsiteURL + "docs/guides/getting-started",
                "Discord" => Shared.DiscordInviteURL,
                "Acknowledgements" => Shared.AcknowledgementsURL,
                _ => null,
            };
            if (url == null)
                return;
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open link {Type}", type);
        }
    }

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
            logger.LogError(ex, "Failed to save language");
            if (revertAction != null)
            {
                try
                {
                    await revertAction();
                }
                catch (Exception revertEx)
                {
                    logger.LogError(revertEx, "Failed to revert language");
                }
            }
        }
    }
}
