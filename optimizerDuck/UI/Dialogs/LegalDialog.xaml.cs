using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using optimizerDuck.UI.ViewModels.Dialogs;

namespace optimizerDuck.UI.Dialogs
{
    public partial class LegalDialog : UserControl
    {
        public LegalDialog()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public LegalDialog(LegalDialogViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;

            if (DataContext is LegalDialogViewModel vm)
            {
                await vm.OnNavigatedToAsync();
                return;
            }

            // Fallback for design-time or direct new LegalDialog() from MainWindow
            // Try to resolve from App.Host
            try
            {
                if (Application.Current is App app && app.AppHost != null)
                {
                    var vm2 = app.AppHost.Services.GetRequiredService<LegalDialogViewModel>();
                    DataContext = vm2;
                    await vm2.OnNavigatedToAsync();
                }
            }
            catch
            {
                // ignore for designer
            }
        }
    }
}
