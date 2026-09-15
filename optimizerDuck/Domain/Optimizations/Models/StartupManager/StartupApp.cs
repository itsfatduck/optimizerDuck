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
    [ObservableProperty]
    private string _command = string.Empty;

    private string? _filePath;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private ImageSource? _logoImage;

    /// <summary>
    ///     The original value name (registry), file name (folder), or task id (packaged app).
    /// </summary>
    [ObservableProperty]
    private string _originalValueNameOrFileName = string.Empty;

    [ObservableProperty]
    private string _publisher = string.Empty;

    public required string Name { get; init; }

    public required StartupAppLocation Location { get; init; }

    public required string PathOrKey { get; init; }

    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (SetProperty(ref _filePath, value))
                OnPropertyChanged(nameof(CanOpenLocation));
        }
    }

    public bool CanOpenLocation => !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);

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
