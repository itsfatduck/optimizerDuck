using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace optimizerDuck.UI.Views.Pages;

public partial class StartupManagerPage : INavigableView<StartupManagerViewModel>
{
    public StartupManagerPage(StartupManagerViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }

    public StartupManagerViewModel ViewModel { get; }
}