using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Models.Cleanup;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class DiskCleanupViewModel(
    DiskCleanupService diskCleanupService,
    ISnackbarService snackbarService,
    ILogger<DiskCleanupViewModel> logger) : ViewModel
{
    [ObservableProperty] private ObservableCollection<CleanupItem> _cleanupItems = [];
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isCleaning;

    // Sort
    [ObservableProperty] private int _selectedSortByIndex; // 0=Default, 1=Size, 2=Name, 3=Path

    private List<CleanupItem> _originalOrder = [];

    partial void OnSelectedSortByIndexChanged(int value) => ApplySort();

    public bool HasData => CleanupItems.Count > 0 && !IsLoading;
    public bool IsAllScanned => CleanupItems.Count > 0 && CleanupItems.All(i => i.IsScanned);
    public int SelectedCount => CleanupItems.Count(i => i.IsSelected);
    public long TotalSelectedSizeBytes => CleanupItems.Where(i => i.IsSelected).Sum(i => i.SizeBytes);
    public string TotalSelectedSizeFormatted => CleanupItem.FormatBytes(TotalSelectedSizeBytes);
    public long TotalSelectedFileCount => CleanupItems.Where(i => i.IsSelected).Sum(i => i.FileCount);
    public long TotalSizeBytes => CleanupItems.Sum(i => i.SizeBytes);
    public string TotalSizeFormatted => CleanupItem.FormatBytes(TotalSizeBytes);
    public long TotalFileCount => CleanupItems.Sum(i => i.FileCount);
    public bool CanClean => SelectedCount > 0 && TotalSelectedSizeBytes > 0 && !IsCleaning && !IsScanning;
    public bool IsAllSelected => CleanupItems.Count > 0 && CleanupItems.All(i => i.IsSelected);

    private bool _initialized;

    public override async Task OnNavigatedToAsync()
    {
        if (_initialized) return;
        _initialized = true;

        IsLoading = true;
        try
        {
            var items = diskCleanupService.GetCleanupItems();
            CleanupItems = new ObservableCollection<CleanupItem>(items);
            _originalOrder = [.. items];

            foreach (var item in CleanupItems)
            {
                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName is nameof(CleanupItem.IsSelected) or nameof(CleanupItem.SizeBytes) or nameof(CleanupItem.FileCount))
                        UpdateProperties();
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load cleanup items");
        }
        finally
        {
            IsLoading = false;
            UpdateProperties();
        }

        // Automatically start scanning
        await ScanAsync();
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsScanning) return;

        IsScanning = true;
        try
        {
            await diskCleanupService.ScanAllAsync(CleanupItems);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to scan cleanup items");
        }
        finally
        {
            IsScanning = false;
            UpdateProperties();
        }
    }

    [RelayCommand]
    private async Task CleanSelectedAsync()
    {
        if (!CanClean) return;

        IsCleaning = true;
        try
        {
            var freedBytes = await diskCleanupService.CleanSelectedAsync(CleanupItems);

            // Deselect all items after successful clean
            foreach (var item in CleanupItems)
                item.IsSelected = false;

            snackbarService.Show(
                Translations.DiskCleanup_Complete_Title,
                string.Format(Translations.DiskCleanup_Complete_Message, CleanupItem.FormatBytes(freedBytes)),
                ControlAppearance.Success,
                new SymbolIcon { Symbol = SymbolRegular.CheckmarkCircle24, Filled = true },
                TimeSpan.FromSeconds(5));

            logger.LogInformation("Disk cleanup completed, freed {Size}", CleanupItem.FormatBytes(freedBytes));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clean selected items");
            snackbarService.Show(
                Translations.DiskCleanup_Error_Title,
                Translations.DiskCleanup_Error_Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5));
        }
        finally
        {
            IsCleaning = false;
            await diskCleanupService.ScanAllAsync(CleanupItems);
            UpdateProperties();
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        var shouldSelect = !IsAllSelected;
        foreach (var item in CleanupItems)
            item.IsSelected = shouldSelect;
        UpdateProperties();
    }

    private void ApplySort()
    {
        var sorted = SelectedSortByIndex switch
        {
            1 => CleanupItems.OrderByDescending(i => i.SizeBytes).ToList(),
            2 => CleanupItems.OrderBy(i => i.Name).ToList(),
            3 => CleanupItems.OrderBy(i => i.Path).ToList(),
            _ => _originalOrder.ToList()
        };

        for (var i = 0; i < sorted.Count; i++)
        {
            var currentIndex = CleanupItems.IndexOf(sorted[i]);
            if (currentIndex != i)
                CleanupItems.Move(currentIndex, i);
        }
    }

    private void UpdateProperties()
    {
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(IsAllScanned));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(TotalSelectedSizeBytes));
        OnPropertyChanged(nameof(TotalSelectedSizeFormatted));
        OnPropertyChanged(nameof(TotalSelectedFileCount));
        OnPropertyChanged(nameof(TotalSizeBytes));
        OnPropertyChanged(nameof(TotalSizeFormatted));
        OnPropertyChanged(nameof(TotalFileCount));
        OnPropertyChanged(nameof(CanClean));
        OnPropertyChanged(nameof(IsAllSelected));
    }
}