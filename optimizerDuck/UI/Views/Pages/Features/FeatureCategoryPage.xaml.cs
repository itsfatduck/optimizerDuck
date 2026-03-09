using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace optimizerDuck.UI.Views.Pages.Features;

public partial class FeatureCategoryPage : INavigableView<FeatureCategoryViewModel>
{
    public FeatureCategoryViewModel ViewModel { get; }

    public FeatureCategoryPage(FeatureCategoryViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }
}
