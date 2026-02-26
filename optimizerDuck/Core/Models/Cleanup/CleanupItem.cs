using CommunityToolkit.Mvvm.ComponentModel;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.Models.Cleanup;

public partial class CleanupItem : ObservableObject
{
    [ObservableProperty] private long _fileCount;
    [ObservableProperty] private bool _isCleaning;
    [ObservableProperty] private bool _isScanned;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isSelected = true;

    [ObservableProperty] private long _sizeBytes;
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Path { get; init; }
    public required SymbolRegular Icon { get; init; }

    /// <summary>
    ///     Whether this item uses a PowerShell command instead of a file path.
    /// </summary>
    public bool IsCommand { get; init; }

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
            _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
        };
    }
}