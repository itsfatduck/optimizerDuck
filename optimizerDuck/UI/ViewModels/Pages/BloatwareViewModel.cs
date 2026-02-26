using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Models.Bloatware;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services;
using optimizerDuck.Services.Managers;
using optimizerDuck.UI.ViewModels;
using optimizerDuck.UI.ViewModels.Dialogs;
using optimizerDuck.UI.Views.Dialogs;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class BloatwareViewModel : ViewModel
{
    private bool _isInitialized;

    private readonly BloatwareService _bloatwareService;
    private readonly IContentDialogService _contentDialogService;
    private readonly ISnackbarService _snackbarService;
    private readonly ILogger<BloatwareViewModel> _logger;

    private readonly List<AppXPackage> _allPackages = [];
    public ObservableCollection<AppXPackage> AppxPackages { get; } = [];
    [ObservableProperty] private bool isLoading;

    // Search, Filter, Sort
    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty] private int _selectedRiskFilterIndex; // 0=All, 1=Safe, 2=Caution
    [ObservableProperty] private int _selectedSortByIndex; // 0=Default, 1=Name, 2=Publisher, 3=Risk

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedRiskFilterIndexChanged(int value) => ApplyFilter();

    partial void OnSelectedSortByIndexChanged(int value) => ApplyFilter();

    public bool HasSelectedItems => AppxPackages.Any(x => x.IsSelected);
    public int SelectedCount => AppxPackages.Count(x => x.IsSelected);
    public bool HasData => _allPackages.Count > 0;
    public bool HasSafePackages => AppxPackages.Any(x => x.Risk == AppRisk.Safe);
    public bool IsAllSafeSelected => HasSafePackages && AppxPackages.Where(x => x.Risk == AppRisk.Safe).All(x => x.IsSelected);

    public BloatwareViewModel(BloatwareService bloatwareService, IContentDialogService contentDialogService,
        ISnackbarService snackbarService, ILogger<BloatwareViewModel> logger)
    {
        _bloatwareService = bloatwareService;
        _contentDialogService = contentDialogService;
        _snackbarService = snackbarService;
        _logger = logger;

        AppxPackages.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
                foreach (AppXPackage p in e.NewItems)
                    p.PropertyChanged += Item_PropertyChanged;

            if (e.OldItems != null)
                foreach (AppXPackage p in e.OldItems)
                    p.PropertyChanged -= Item_PropertyChanged;

            UpdateProperties();
            RemoveSelectedCommand.NotifyCanExecuteChanged();
        };
    }

    public override async Task OnNavigatedToAsync()
    {
        if (_isInitialized)
            return;

        _isInitialized = true;

        IsLoading = true;
        var appxPackages = await _bloatwareService.GetAppXPackagesAsync();
        _allPackages.AddRange(appxPackages);
        ApplyFilter();
        IsLoading = false;
    }

    private void ApplyFilter()
    {
        var query = _allPackages.AsEnumerable();

        // Search
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            query = query.Where(p =>
                p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Publisher.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.PackageFullName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        // Filter by risk
        query = SelectedRiskFilterIndex switch
        {
            1 => query.Where(p => p.Risk == AppRisk.Safe),
            2 => query.Where(p => p.Risk == AppRisk.Caution),
            _ => query
        };

        // Sort
        query = SelectedSortByIndex switch
        {
            1 => query.OrderBy(p => p.Name),
            2 => query.OrderBy(p => p.Publisher),
            3 => query.OrderBy(p => p.Risk),
            _ => query
        };

        // Detach PropertyChanged listeners before clearing to avoid unnecessary event overhead
        foreach (var p in AppxPackages)
            p.PropertyChanged -= Item_PropertyChanged;

        var filtered = query.ToList();
        AppxPackages.Clear();
        foreach (var package in filtered)
            AppxPackages.Add(package);
    }

    #region Commands

    [RelayCommand(CanExecute = nameof(CanRemoveSelected))]
    private async Task RemoveSelected()
    {
        var toRemove = AppxPackages.Where(x => x.IsSelected).ToList();

        var askForConfirmationDialog = new ContentDialog
        {
            Title = string.Format(Translations.BloatwareDialog_Confirmation_Title, SelectedCount),
            Content = string.Format(Translations.BloatwareDialog_Confirmation_Message, string.Join(", ", toRemove.Select(a => a.Name))),
            PrimaryButtonText = Translations.Button_Ok,
            CloseButtonText = Translations.Button_Cancel
        };

        var result = await _contentDialogService.ShowAsync(askForConfirmationDialog, CancellationToken.None);
        if (result != ContentDialogResult.Primary)
            return;

        var viewModel = new ProcessingViewModel();
        var dialog = new ContentDialog
        {
            Title = string.Format(Translations.BloatwareDialog_Title, toRemove.Count),
            Content = new ProcessingDialog { DataContext = viewModel },
            IsFooterVisible = false
        };

        try
        {
            _logger.LogInformation("===== START removing {Count} AppX Packages =====", toRemove.Count);
            _ = _contentDialogService.ShowAsync(dialog, CancellationToken.None);

            for (var i = 0; i < toRemove.Count; i++)
            {
                var item = toRemove[i];

                viewModel.ProgressReporter.Report(new ProcessingProgress
                {
                    Message = string.Format(Translations.BloatwareDialog_Removing, item.Name, i + 1, toRemove.Count),
                    IsIndeterminate = false,
                    Total = toRemove.Count,
                    Value = i
                });
                await _bloatwareService.RemoveAppXPackage(item);
                //await Task.Delay(500); // Simulate work
            }
        }
        finally
        {
            _logger.LogInformation("===== END removing {Count} AppX Packages =====", toRemove.Count);
            dialog.Hide();
            await Refresh();
        }
    }

    private bool CanRemoveSelected() => HasSelectedItems;

    [RelayCommand]
    private async Task Refresh()
    {
        IsLoading = true;
        _allPackages.Clear();
        AppxPackages.Clear();
        UpdateProperties();

        var appxPackages = await _bloatwareService.GetAppXPackagesAsync();
        _allPackages.AddRange(appxPackages);
        ApplyFilter();
        IsLoading = false;
    }

    [RelayCommand]
    private void SelectAllSafe()
    {
        var shouldDeselect = IsAllSafeSelected;
        foreach (var package in AppxPackages.Where(x => x.Risk == AppRisk.Safe))
            package.IsSelected = !shouldDeselect;
    }

    #endregion Commands

    #region Property Changed

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppXPackage.IsSelected))
        {
            UpdateProperties();
            RemoveSelectedCommand.NotifyCanExecuteChanged();
        }
    }

    #endregion Property Changed

    #region Helpers

    private void UpdateProperties()
    {
        OnPropertyChanged(nameof(HasSelectedItems));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(HasSafePackages));
        OnPropertyChanged(nameof(IsAllSafeSelected));
    }

    #endregion Helpers
}