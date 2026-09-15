using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Services.Configuration;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Optimizations.Models.Cleanup;

/// <summary>
///     Represents a disk cleanup item (e.g., temp files, Windows Update cache).
public partial class CleanupItem : LocalizedObject
{
    [ObservableProperty]
    private long _fileCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowActions))]
    private bool _isCleaning;

    [ObservableProperty]
    private bool _isScanned;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowActions))]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isSelected = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowActions))]
    private long _sizeBytes;

    public required string Id { get; init; }

    /// <summary>
    ///     Gets the display name resolved from <see cref="NameKey" /> for the current culture.
    /// </summary>
    public string Name => Loc.Instance[NameKey];

    /// <summary>
    ///     Gets the description resolved from <see cref="DescriptionKey" /> for the
    ///     current culture.
    /// </summary>
    public string Description => Loc.Instance[DescriptionKey];

    /// <summary>Resource key that localizes <see cref="Name" />.</summary>
    public string NameKey { get; init; } = string.Empty;

    /// <summary>Resource key that localizes <see cref="Description" />.</summary>
    public string DescriptionKey { get; init; } = string.Empty;

    public required string Path { get; init; }

    public required SymbolRegular Icon { get; init; }

    /// <summary>
    ///     Whether this item uses a PowerShell command instead of a file path.
    /// </summary>
    public bool IsCommand { get; init; }

    public bool CanOpenFolder => !IsCommand && System.IO.Directory.Exists(Path);

    public bool ShowActions => SizeBytes > 0 && !IsScanning && !IsCleaning;

    public string FormattedSize => FormatBytes(SizeBytes);

    partial void OnSizeBytesChanged(long value)
    {
        OnPropertyChanged(nameof(FormattedSize));
    }

    public static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
            _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB",
        };
    }
}
