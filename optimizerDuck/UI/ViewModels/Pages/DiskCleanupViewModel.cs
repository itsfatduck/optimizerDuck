using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.UI;
using optimizerDuck.UI.Dialogs;
using optimizerDuck.UI.ViewModels.Dialogs;
using Wpf.Ui;
using Wpf.Ui.Controls;
using CleanupItem = optimizerDuck.Domain.Optimizations.Models.Cleanup.CleanupItem;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class DiskCleanupViewModel(
    DiskCleanupService diskCleanupService,
    ISnackbarService snackbarService,
    IContentDialogService contentDialogService,
    ILogger<DiskCleanupViewModel> logger
) : ViewModel
{
    [ObservableProperty]
    private ObservableCollection<CleanupItem> _cleanupItems = [];

    [ObservableProperty]
    private bool _isCleaning;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isScanning;

    private List<CleanupItem> _originalOrder = [];

    // Sort
    [ObservableProperty]
    private int _selectedSortByIndex; // 0=Size, 1=Name, 2=Path

    public bool HasData => CleanupItems.Count > 0 && !IsLoading;
    public bool IsAllScanned => CleanupItems.Count > 0 && CleanupItems.All(i => i.IsScanned);
    public int SelectedCount => CleanupItems.Count(i => i.IsSelected);
    public long TotalSelectedSizeBytes =>
        CleanupItems.Where(i => i.IsSelected).Sum(i => i.SizeBytes);
    public string TotalSelectedSizeFormatted => CleanupItem.FormatBytes(TotalSelectedSizeBytes);
    public long TotalSelectedFileCount =>
        CleanupItems.Where(i => i.IsSelected).Sum(i => i.FileCount);
    public long TotalSizeBytes => CleanupItems.Sum(i => i.SizeBytes);
    public string TotalSizeFormatted => CleanupItem.FormatBytes(TotalSizeBytes);
    public long TotalFileCount => CleanupItems.Sum(i => i.FileCount);
    public bool CanClean =>
        SelectedCount > 0 && TotalSelectedSizeBytes > 0 && !IsCleaning && !IsScanning;
    public bool IsAllSelected =>
        CleanupItems.Count > 0 && CleanupItems.All(i => !i.IsScanned || i.SizeBytes == 0 || i.IsSelected);

    partial void OnSelectedSortByIndexChanged(int value)
    {
        ApplySort();
    }

    protected override async Task InitializeOnceAsync()
    {
        IsLoading = true;
        try
        {
            var items = DiskCleanupService.GetCleanupItems();
            CleanupItems = new ObservableCollection<CleanupItem>(items.ToArray());
            _originalOrder = [.. items];

            foreach (var item in CleanupItems)
                item.PropertyChanged += (_, e) =>
                {
                    if (
                        e.PropertyName
                        is nameof(CleanupItem.IsSelected)
                            or nameof(CleanupItem.SizeBytes)
                            or nameof(CleanupItem.FileCount)
                    )
                        UpdateProperties();
                };
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
    }

    public override async Task OnNavigatedToAsync()
    {
        await base.OnNavigatedToAsync();

        await ScanAsync();

        CleanupItems = new ObservableCollection<CleanupItem>(
            CleanupItems.OrderByDescending(i => i.SizeBytes).ToArray()
        );

        foreach (var item in CleanupItems)
            if (item.SizeBytes == 0)
                item.IsSelected = false;

        UpdateProperties();
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsScanning)
            return;

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
    private async Task CleanItemAsync(CleanupItem item)
    {
        if (item.SizeBytes <= 0)
            return;

        try
        {
            var sw = Stopwatch.StartNew();

            var freedBytes = await diskCleanupService.CleanAsync(item);
            sw.Stop();

            snackbarService.Show(
                Translations.DiskCleanup_Complete_Title,
                string.Format(
                    Translations.DiskCleanup_Complete_Message,
                    CleanupItem.FormatBytes(freedBytes),
                    $"{sw.Elapsed.TotalSeconds:0.0}s"
                ),
                ControlAppearance.Success,
                new SymbolIcon { Symbol = SymbolRegular.CheckmarkCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );

            item.IsSelected = false;

            logger.LogInformation(
                "Disk cleanup completed, freed {Size} in {Duration}",
                CleanupItem.FormatBytes(freedBytes),
                sw.Elapsed
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clean item {ItemId}", item.Id);
            snackbarService.Show(
                Translations.DiskCleanup_Error_Title,
                Translations.DiskCleanup_Error_Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
        }
        finally
        {
            await diskCleanupService.ScanAllAsync(CleanupItems);

            UpdateProperties();
            ApplySort();
        }
    }

    [RelayCommand]
    private async Task CleanSelectedAsync()
    {
        if (!CanClean)
            return;

        var selectedItems = CleanupItems.Where(i => i.IsSelected && i.SizeBytes > 0).ToList();
        if (selectedItems.Count == 0)
            return;

        var summaryText = string.Format(
            Translations.DiskCleanup_Dialog_Confirmation_Summary,
            TotalSelectedSizeFormatted,
            SelectedCount,
            TotalSelectedFileCount
        );

        var dialogViewModel = new DiskCleanupConfirmationDialogViewModel(selectedItems, summaryText);
        var dialog = new ContentDialog
        {
            Title = string.Format(Translations.DiskCleanup_Dialog_Confirmation_Title, SelectedCount),
            Content = new DiskCleanupConfirmationDialog { DataContext = dialogViewModel },
            PrimaryButtonText = string.Format(Translations.DiskCleanup_Button_CleanSelected, TotalSelectedSizeFormatted),
            CloseButtonText = Translations.Common_Cancel,
        };

        var result = await contentDialogService.ShowAsync(dialog, CancellationToken.None);
        if (result != ContentDialogResult.Primary)
            return;

        IsCleaning = true;
        try
        {
            var sw = Stopwatch.StartNew();
            var freedBytes = await diskCleanupService.CleanSelectedAsync(CleanupItems);
            sw.Stop();

            // Deselect all items after successful clean
            foreach (var item in CleanupItems)
                item.IsSelected = false;

            snackbarService.Show(
                Translations.DiskCleanup_Complete_Title,
                string.Format(
                    Translations.DiskCleanup_Complete_Message,
                    CleanupItem.FormatBytes(freedBytes),
                    $"{sw.Elapsed.TotalSeconds:0.0}s"
                ),
                ControlAppearance.Success,
                new SymbolIcon { Symbol = SymbolRegular.CheckmarkCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );

            logger.LogInformation(
                "Disk cleanup completed, freed {Size} in {Duration}",
                CleanupItem.FormatBytes(freedBytes),
                sw.Elapsed
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clean selected items");
            snackbarService.Show(
                Translations.DiskCleanup_Error_Title,
                Translations.DiskCleanup_Error_Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
        }
        finally
        {
            IsCleaning = false;
            await diskCleanupService.ScanAllAsync(CleanupItems);
            UpdateProperties();
            ApplySort();
        }
    }

    [RelayCommand]
    private async Task RefreshItemAsync(CleanupItem item)
    {
        if (item is null)
            return;

        await diskCleanupService.ScanAsync(item);

        if (item.SizeBytes == 0)
            item.IsSelected = false;

        UpdateProperties();
        ApplySort();
    }

    [RelayCommand]
    private void ToggleSelectAll()
    {
        if (IsAllSelected)
            DeselectAll();
        else
            SelectAll();
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in CleanupItems)
            if (item.IsScanned && item.SizeBytes > 0)
                item.IsSelected = true;

        UpdateProperties();
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var item in CleanupItems)
            item.IsSelected = false;

        UpdateProperties();
    }

    [RelayCommand]
    private void OpenFolder(CleanupItem item)
    {
        if (item?.CanOpenFolder != true)
            return;

        try
        {
            Process.Start(new ProcessStartInfo { FileName = item.Path, UseShellExecute = true });
            logger.LogInformation("Opened folder: {Path}", item.Path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open folder: {Path}", item.Path);
            snackbarService.Show(
                Translations.Snackbar_OpenFailed_Title,
                Translations.Snackbar_OpenFailed_Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(3)
            );
        }
    }

    private void ApplySort()
    {
        var sorted = SelectedSortByIndex switch
        {
            0 => CleanupItems.OrderByDescending(i => i.SizeBytes).ToList(),
            1 => CleanupItems.OrderBy(i => i.Name).ToList(),
            2 => CleanupItems.OrderBy(i => i.Path).ToList(),
            _ => _originalOrder.ToList(),
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
