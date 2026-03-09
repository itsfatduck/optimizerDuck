using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace optimizerDuck.UI.Views.Pages;

public partial class FeaturesPage : INavigableView<FeaturesViewModel>
{
    public FeaturesViewModel ViewModel { get; }

    public FeaturesPage(FeaturesViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();

        Loaded += async (_, _) => await ViewModel.InitializeAsync();
    }
}
