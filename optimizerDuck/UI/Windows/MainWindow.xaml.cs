using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Configuration;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.Customize;
using optimizerDuck.UI.Controls;
using optimizerDuck.UI.Dialogs;
using optimizerDuck.UI.Pages;
using optimizerDuck.UI.ViewModels.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.Windows;

public partial class MainWindow : IWindow
{
    private readonly ILogger<MainWindow> _logger;
    private readonly IContentDialogService _contentDialogService;
    private readonly IOptionsMonitor<AppSettings> _appOptionsMonitor;
    private readonly ConfigManager _configManager;

    public MainWindow(
        MainWindowViewModel viewModel,
        ConfigManager configManager,
        INavigationService navigationService,
        IContentDialogService contentDialogService,
        INavigationViewPageProvider pageProvider,
        IOptionsMonitor<AppSettings> appOptionsMonitor,
        ISnackbarService snackbarService,
        CustomizeRegistry customizeRegistry,
        ILogger<MainWindow> logger
    )
    {
        _logger = logger;
        _contentDialogService = contentDialogService;
        _appOptionsMonitor = appOptionsMonitor;
        _configManager = configManager;
        InitializeComponent();

        DataContext = viewModel;

        snackbarService.SetSnackbarPresenter(SnackbarPresenter);
        contentDialogService.SetDialogHost(RootContentDialogPresenter);
        navigationService.SetNavigationControl(RootNavigation);

        RootNavigation.SetPageProviderService(pageProvider);

        RootNavigation.Loaded += OnRootNavigationLoaded;
    }

    internal void UpdatePendingIndicator(bool hasPending)
    {
        AppTitleText.Text = hasPending ? "optimizerDuck*" : "optimizerDuck";
        Title = hasPending ? "optimizerDuck*" : "optimizerDuck";
    }

    private async void OnRootNavigationLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            RootNavigation.Loaded -= OnRootNavigationLoaded;

            if (!_appOptionsMonitor.CurrentValue.App.LegalAccepted)
            {
                var legalDialog = new LegalDialog();
                var dialog = new ContentDialog
                {
                    Title = Translations.LegalDialog_Title,
                    Content = legalDialog,
                    PrimaryButtonText = Translations.Button_Accept,
                    CloseButtonText = Translations.Button_Close,
                    DefaultButton = ContentDialogButton.Primary,
                };

                var result = await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
                if (result == ContentDialogResult.Primary)
                {
                    await _configManager.SetAsync(x => x.App.LegalAccepted, true);
                }
                else
                {
                    Close();
                    return;
                }
            }

            RootNavigation.Navigate(typeof(DashboardPage));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during navigation initialization");
        }
    }
}
