using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using optimizerDuck.Core.Models.Bloatware;
using optimizerDuck.Services;
using optimizerDuck.UI.ViewModels;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class BloatwareViewModel : ViewModel
{
    private bool _isInitialized;

    private readonly BloatwareService _bloatwareService;


    public ObservableCollection<AppxPackage> AppxPackages { get; set; } = [];

    public bool HasSelectedItems => AppxPackages.Any(x => x.IsSelected);
    public int SelectedCount => AppxPackages.Count(x => x.IsSelected);

    public BloatwareViewModel(BloatwareService bloatwareService)
    {
        _bloatwareService = bloatwareService;

        AppxPackages.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
                foreach (AppxPackage p in e.NewItems)
                    p.PropertyChanged += Item_PropertyChanged;

            if (e.OldItems != null)
                foreach (AppxPackage p in e.OldItems)
                    p.PropertyChanged -= Item_PropertyChanged;

            UpdateProperties();
            RemoveSelectedCommand.NotifyCanExecuteChanged();
        };
    }

    public override async Task OnNavigatedToAsync()
    {
        if (_isInitialized)
            return;

        var appxPackages = await _bloatwareService.GetAppXPackagesAsync();
        foreach (var package in appxPackages)
            AppxPackages.Add(package);

        _isInitialized = true;
    }

    #region Commands

    [RelayCommand(CanExecute = nameof(CanRemoveSelected))]
    private void RemoveSelected()
    {
        var toRemove = AppxPackages.Where(x => x.IsSelected).ToList();

        foreach (var item in toRemove)
            AppxPackages.Remove(item);
    }

    private bool CanRemoveSelected() => HasSelectedItems;

    #endregion

    #region Property Changed

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppxPackage.IsSelected))
        {
            UpdateProperties();
            RemoveSelectedCommand.NotifyCanExecuteChanged();
        }
    }

    #endregion

    #region Helpers

    private void UpdateProperties()
    {
        OnPropertyChanged(nameof(HasSelectedItems));
        OnPropertyChanged(nameof(SelectedCount));
    }

    #endregion
}