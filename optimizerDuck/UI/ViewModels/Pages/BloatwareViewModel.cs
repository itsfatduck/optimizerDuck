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

    public ObservableCollection<AppXPackage> AppxPackages { get; } = [];
    [ObservableProperty] private bool isLoading;

    public bool HasSelectedItems => AppxPackages.Any(x => x.IsSelected);
    public int SelectedCount => AppxPackages.Count(x => x.IsSelected);

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
        foreach (var package in appxPackages)
            AppxPackages.Add(package);
        IsLoading = false;
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
        AppxPackages.Clear();
        UpdateProperties();

        var appxPackages = await _bloatwareService.GetAppXPackagesAsync();
        foreach (var package in appxPackages)
            AppxPackages.Add(package);
        IsLoading = false;
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
    }

    #endregion Helpers
}