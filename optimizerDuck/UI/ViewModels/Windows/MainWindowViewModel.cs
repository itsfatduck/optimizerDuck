using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using optimizerDuck.Common.Helpers;

namespace optimizerDuck.UI.ViewModels.Windows;

public partial class MainWindowViewModel : ObservableObject
{
    /// <summary>
    ///     Gets the application version displayed in the title bar.
    /// </summary>
    public string Version => $"[v{Shared.FileVersion}]";

    /// <summary>
    ///     Opens the support/donation link in the default browser.
    /// </summary>
    [RelayCommand]
    private static void OpenSupportLink()
    {
        try
        {
            Process.Start(
                new ProcessStartInfo { FileName = Shared.ContributeURL, UseShellExecute = true }
            );
        }
        catch
        {
            // Silently fail - link opening is a non-critical action
        }
    }

    /// <summary>
    ///     Opens the Discord invite link in the default browser.
    /// </summary>
    [RelayCommand]
    private static void OpenDiscordLink()
    {
        try
        {
            Process.Start(
                new ProcessStartInfo { FileName = Shared.DiscordInviteURL, UseShellExecute = true }
            );
        }
        catch
        {
            // Silently fail - link opening is a non-critical action
        }
    }
}
