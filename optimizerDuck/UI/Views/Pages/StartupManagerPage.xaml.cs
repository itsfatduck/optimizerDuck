using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.Views.Pages;

public partial class StartupManagerPage : INavigableView<StartupManagerViewModel>
{
    public StartupManagerViewModel ViewModel { get; }

    public StartupManagerPage(StartupManagerViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }
}