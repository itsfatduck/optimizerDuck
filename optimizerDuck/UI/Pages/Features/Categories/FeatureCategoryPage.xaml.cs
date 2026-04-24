using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace optimizerDuck.UI.Pages.Features;

public partial class FeatureCategoryPage : INavigableView<FeatureCategoryViewModel>
{
    public FeatureCategoryPage(FeatureCategoryViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }

    public FeatureCategoryViewModel ViewModel { get; }
}
