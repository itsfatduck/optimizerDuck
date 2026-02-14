using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using optimizerDuck.Core.Models.Bloatware;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Services;
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


    public ObservableCollection<AppXPackage> AppxPackages { get; private set; } = [];

    public bool HasSelectedItems => AppxPackages.Any(x => x.IsSelected);
    public int SelectedCount => AppxPackages.Count(x => x.IsSelected);

    public BloatwareViewModel(BloatwareService bloatwareService, IContentDialogService contentDialogService)
    {
        _bloatwareService = bloatwareService;
        _contentDialogService = contentDialogService;

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

        var appxPackages = await _bloatwareService.GetAppXPackagesAsync();
        foreach (var package in appxPackages)
            AppxPackages.Add(package);

        _isInitialized = true;
    }

    #region Commands

    [RelayCommand(CanExecute = nameof(CanRemoveSelected))]
    private async Task RemoveSelected()
    {
        var viewModel = new ProcessingViewModel();
        var dialog = new ContentDialog
        {
            Title = "Removing bloatware!",
            Content = new ProcessingDialog { DataContext = viewModel },
            IsFooterVisible = false
        };

        try
        {
            _ = _contentDialogService.ShowAsync(dialog, CancellationToken.None);
        
            var toRemove = AppxPackages.Where(x => x.IsSelected).ToList();

            for (var i = 0; i < toRemove.Count; i++)
            {
                var item = toRemove[i];
                viewModel.ProgressReporter.Report(new ProcessingProgress
                {
                    Message = $"{i + 1} / {toRemove.Count}"
                });
                //await _bloatwareService.RemoveAppXPackage(item);
                await Task.Delay(1000);
            }
        }
        finally
        {
            dialog.Hide();
            await Refresh();
        }
    }

    private bool CanRemoveSelected() => HasSelectedItems;

    [RelayCommand]
    private async Task Refresh()
    {
        AppxPackages.Clear();
        UpdateProperties();

        var appxPackages = await _bloatwareService.GetAppXPackagesAsync();
        foreach (var package in appxPackages)
            AppxPackages.Add(package);
    }


    #endregion

    #region Property Changed

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppXPackage.IsSelected))
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