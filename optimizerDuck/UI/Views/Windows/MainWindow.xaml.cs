using System.Windows;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.UI.Views.Pages;
using Wpf.Ui;
using Wpf.Ui.Abstractions;

namespace optimizerDuck.UI.Views.Windows;

public partial class MainWindow : IWindow
{
    public MainWindow(INavigationService navigationService, IContentDialogService contentDialogService,
        INavigationViewPageProvider pageProvider, ISnackbarService snackbarService)
    {
        InitializeComponent();

        snackbarService.SetSnackbarPresenter(SnackbarPresenter);
        contentDialogService.SetDialogHost(RootContentDialogPresenter);
        navigationService.SetNavigationControl(RootNavigation);

        RootNavigation.SetPageProviderService(pageProvider);

        RootNavigation.Loaded += OnRootNavigationLoaded;
    }

    private void OnRootNavigationLoaded(object sender, RoutedEventArgs e)
    {
        RootNavigation.Loaded -= OnRootNavigationLoaded;
        RootNavigation.Navigate(typeof(DashboardPage));
    }
}