using System.IO;
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

    [ObservableProperty] private ImageSource? _logoImage;
    public required string Name { get; init; }
    [ObservableProperty] private string _publisher = string.Empty;
    [ObservableProperty] private string _command = string.Empty;
    public required StartupAppLocation Location { get; init; }
    public required string PathOrKey { get; init; }
    [ObservableProperty] private string _originalValueNameOrFileName = string.Empty;
    
    private string? _filePath;
    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (SetProperty(ref _filePath, value))
            {
                OnPropertyChanged(nameof(CanOpenLocation));
            }
        }
    }

    public bool CanOpenLocation => !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);

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