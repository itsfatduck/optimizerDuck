using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace optimizerDuck.UI.Pages;

public partial class CustomizePage : INavigableView<CustomizeViewModel>
{
    public CustomizePage(CustomizeViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();

        Loaded += async (_, _) => await ViewModel.InitializeAsync();
    }

    public CustomizeViewModel ViewModel { get; }
}
