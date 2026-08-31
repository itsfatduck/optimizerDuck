using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Domain.Optimizations.Models.StartupManager;

/// <summary>
///     Specifies the location of a startup application.
/// </summary>
public enum StartupAppLocation
{
    RegistryHKCURun,
    RegistryHKLMRun,
    RegistryHKCURunOnce,
    RegistryHKLMRunOnce,

    /// <summary>32-bit registry view of HKLM Run (Wow6432Node).</summary>
    RegistryHKLMRun32,

    /// <summary>32-bit registry view of HKLM RunOnce (Wow6432Node).</summary>
    RegistryHKLMRunOnce32,

    UserStartupFolder,
    CommonStartupFolder,

    /// <summary>Startup task declared by a packaged (UWP / MSIX) app.</summary>
    UwpStartupTask,
}

/// <summary>
///     Represents an application that runs at Windows startup.
/// </summary>
public partial class StartupApp : LocalizedObject
{
    /// <summary>
    ///     The command that runs the application.
    /// </summary>
    [ObservableProperty]
    private string _command = string.Empty;

    private string? _filePath;

    /// <summary>
    ///     Indicates whether this startup entry is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isEnabled;

    /// <summary>
    ///     The logo image of the application.
    /// </summary>
    [ObservableProperty]
    private ImageSource? _logoImage;

    /// <summary>
    ///     The original value name (registry), file name (folder), or task id (packaged app).
    /// </summary>
    [ObservableProperty]
    private string _originalValueNameOrFileName = string.Empty;

    /// <summary>
    ///     The publisher or company name.
    /// </summary>
    [ObservableProperty]
    private string _publisher = string.Empty;

    /// <summary>
    ///     The display name of the startup application.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Where this startup entry is located.
    /// </summary>
    public required StartupAppLocation Location { get; init; }

    /// <summary>
    ///     The registry key path or folder path.
    /// </summary>
    public required string PathOrKey { get; init; }

    /// <summary>
    ///     The actual file path of the executable.
    /// </summary>
    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (SetProperty(ref _filePath, value))
                OnPropertyChanged(nameof(CanOpenLocation));
        }
    }

    /// <summary>
    ///     Indicates whether the file location can be opened.
    /// </summary>
    public bool CanOpenLocation => !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);

    /// <summary>
    ///     Gets a human-readable string for the location.
    /// </summary>
    public string LocationDisplay =>
        LocationDisplayKeys.TryGetValue(Location, out var key) ? Loc.Instance[key] : PathOrKey;

    private static readonly Dictionary<StartupAppLocation, string> LocationDisplayKeys = new()
    {
        [StartupAppLocation.RegistryHKCURun] = "Startup.Location.RegistryHKCURun",
        [StartupAppLocation.RegistryHKLMRun] = "Startup.Location.RegistryHKLMRun",
        [StartupAppLocation.RegistryHKCURunOnce] = "Startup.Location.RegistryHKCURunOnce",
        [StartupAppLocation.RegistryHKLMRunOnce] = "Startup.Location.RegistryHKLMRunOnce",
        [StartupAppLocation.RegistryHKLMRun32] = "Startup.Location.RegistryHKLMRun32",
        [StartupAppLocation.RegistryHKLMRunOnce32] = "Startup.Location.RegistryHKLMRunOnce32",
        [StartupAppLocation.UserStartupFolder] = "Startup.Location.UserStartupFolder",
        [StartupAppLocation.CommonStartupFolder] = "Startup.Location.CommonStartupFolder",
        [StartupAppLocation.UwpStartupTask] = "Startup.Location.UwpStartupTask",
    };
}
