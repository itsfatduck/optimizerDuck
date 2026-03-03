using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace optimizerDuck.Core.Models.StartupManager;

/// <summary>
///     Specifies the location of a startup application.
/// </summary>
public enum StartupAppLocation
{
    RegistryHKCURun,
    RegistryHKLMRun,
    RegistryHKCURunOnce,
    RegistryHKLMRunOnce,
    UserStartupFolder,
    CommonStartupFolder
}

/// <summary>
///     Represents an application that runs at Windows startup.
/// </summary>
public partial class StartupApp : ObservableObject
{
    /// <summary>
    ///     Indicates whether this startup entry is enabled.
    /// </summary>
    [ObservableProperty] private bool _isEnabled;

    /// <summary>
    ///     The logo image of the application.
    /// </summary>
    [ObservableProperty] private ImageSource? _logoImage;
    /// <summary>
    ///     The display name of the startup application.
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    ///     The publisher or company name.
    /// </summary>
    [ObservableProperty] private string _publisher = string.Empty;
    /// <summary>
    ///     The command that runs the application.
    /// </summary>
    [ObservableProperty] private string _command = string.Empty;
    /// <summary>
    ///     Where this startup entry is located.
    /// </summary>
    public required StartupAppLocation Location { get; init; }
    /// <summary>
    ///     The registry key path or folder path.
    /// </summary>
    public required string PathOrKey { get; init; }
    /// <summary>
    ///     The original value name (registry) or file name (folder).
    /// </summary>
    [ObservableProperty] private string _originalValueNameOrFileName = string.Empty;
    
    private string? _filePath;
    /// <summary>
    ///     The actual file path of the executable.
    /// </summary>
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

    /// <summary>
    ///     Indicates whether the file location can be opened.
    /// </summary>
    public bool CanOpenLocation => !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);

    /// <summary>
    ///     Gets a human-readable string for the location.
    /// </summary>
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