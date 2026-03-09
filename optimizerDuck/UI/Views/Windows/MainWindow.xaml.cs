using System.Windows;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Services;
using optimizerDuck.UI.Views.Pages;
using optimizerDuck.UI.Views.Pages.Features;
using Wpf.Ui;
using Wpf.Ui.Abstractions;

namespace optimizerDuck.UI.Views.Windows;

public partial class MainWindow : IWindow
{
    public MainWindow(INavigationService navigationService, IContentDialogService contentDialogService,
        INavigationViewPageProvider pageProvider, ISnackbarService snackbarService, FeatureRegistry featureRegistry)
    {
        InitializeComponent();

        snackbarService.SetSnackbarPresenter(SnackbarPresenter);
        contentDialogService.SetDialogHost(RootContentDialogPresenter);
        navigationService.SetNavigationControl(RootNavigation);

        RootNavigation.SetPageProviderService(pageProvider);

        var toggleItems = featureRegistry.GetNavigationItems();
        foreach (var item in toggleItems)
        {
            FeaturesMenuItem.MenuItems.Add(item);
        }

        RootNavigation.Loaded += OnRootNavigationLoaded;
    }

    private void OnRootNavigationLoaded(object sender, RoutedEventArgs e)
    {
        RootNavigation.Loaded -= OnRootNavigationLoaded;
        RootNavigation.Navigate(typeof(DashboardPage));
    }
}