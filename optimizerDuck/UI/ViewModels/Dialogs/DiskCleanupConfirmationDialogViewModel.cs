    using optimizerDuck.Domain.Optimizations.Models.Cleanup;

namespace optimizerDuck.UI.ViewModels.Dialogs;

public sealed class DiskCleanupConfirmationDialogViewModel
{
    public DiskCleanupConfirmationDialogViewModel(
        IEnumerable<CleanupItem> selectedItems,
        string summaryText
    )
    {
        SummaryText = summaryText;
        Items = selectedItems
            .Select((item, index) => new DiskCleanupConfirmationItemViewModel(index + 1, item))
            .ToList();
    }

    public string SummaryText { get; }
    public IReadOnlyList<DiskCleanupConfirmationItemViewModel> Items { get; }
}

public sealed class DiskCleanupConfirmationItemViewModel(int index, CleanupItem item)
{
    public int Index { get; } = index;
    public string Name { get; } = item.Name;
    public string Description { get; } = item.Description;
    public string Path { get; } = item.Path;
    public long SizeBytes { get; } = item.SizeBytes;
    public string FormattedSize { get; } = CleanupItem.FormatBytes(item.SizeBytes);
    public long FileCount { get; } = item.FileCount;
}
