using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace optimizerDuck.Core.Models.StartupManager;

public enum StartupAppLocation
{
    RegistryHKCURun,
    RegistryHKLMRun,
    RegistryHKCURunOnce,
    RegistryHKLMRunOnce,
    UserStartupFolder,
    CommonStartupFolder
}

public partial class StartupApp : ObservableObject
{
    [ObservableProperty] private bool _isEnabled;

    public ImageSource? LogoImage { get; set; }
    public required string Name { get; init; }
    public required string Publisher { get; init; }
    public required string Command { get; init; }
    public required StartupAppLocation Location { get; init; }
    public required string PathOrKey { get; init; }
    public required string OriginalValueNameOrFileName { get; init; }

    public string LocationDisplay => Location switch
    {
        StartupAppLocation.RegistryHKCURun => "Registry (Current User)",
        StartupAppLocation.RegistryHKLMRun => "Registry (Local Machine)",
        StartupAppLocation.RegistryHKCURunOnce => "Registry RunOnce (Current User)",
        StartupAppLocation.RegistryHKLMRunOnce => "Registry RunOnce (Local Machine)",
        StartupAppLocation.UserStartupFolder => "Startup Folder (Current User)",
        StartupAppLocation.CommonStartupFolder => "Startup Folder (All Users)",
        _ => PathOrKey
    };
}
