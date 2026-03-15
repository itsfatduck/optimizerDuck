using CommunityToolkit.Mvvm.ComponentModel;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Optimizations.Models.Cleanup;

/// <summary>
///     Represents a disk cleanup item (e.g., temp files, Windows Update cache).
/// </summary>
public partial class CleanupItem : ObservableObject
{
    /// <summary>
    ///     The number of files in this cleanup item.
    /// </summary>
    [ObservableProperty] private long _fileCount;

    /// <summary>
    ///     Indicates whether cleanup is in progress.
    /// </summary>
    [ObservableProperty] private bool _isCleaning;

    /// <summary>
    ///     Indicates whether this item has been scanned.
    /// </summary>
    [ObservableProperty] private bool _isScanned;

    /// <summary>
    ///     Indicates whether scanning is in progress.
    /// </summary>
    [ObservableProperty] private bool _isScanning;

    /// <summary>
    ///     Indicates whether this item is selected for cleanup.
    /// </summary>
    [ObservableProperty] private bool _isSelected = true;

    /// <summary>
    ///     The size of files in bytes.
    /// </summary>
    [ObservableProperty] private long _sizeBytes;

    /// <summary>
    ///     Unique identifier for this cleanup item.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    ///     Display name of the cleanup item.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Description of what this cleanup item contains.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    ///     The file path or folder to clean.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    ///     The icon to display in the UI.
    /// </summary>
    public required SymbolRegular Icon { get; init; }

    /// <summary>
    ///     Whether this item uses a PowerShell command instead of a file path.
    /// </summary>
    public bool IsCommand { get; init; }

    /// <summary>
    ///     Gets the human-readable size string (e.g., "1.5 GB").
    /// </summary>
    public string FormattedSize => FormatBytes(SizeBytes);

    partial void OnSizeBytesChanged(long value)
    {
        OnPropertyChanged(nameof(FormattedSize));
    }

    /// <summary>
    ///     Formats bytes into a human-readable string.
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
            _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
        };
    }
}