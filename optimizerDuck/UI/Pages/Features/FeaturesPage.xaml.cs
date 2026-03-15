using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace optimizerDuck.UI.Pages;

public partial class FeaturesPage : INavigableView<FeaturesViewModel>
{
    public FeaturesPage(FeaturesViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();

        Loaded += async (_, _) => await ViewModel.InitializeAsync();
    }

    public FeaturesViewModel ViewModel { get; }
}