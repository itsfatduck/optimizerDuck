using System.Windows.Controls;
using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace optimizerDuck.UI.Views.Pages.ToggleFeatures;

public partial class ToggleFeaturesCategoryPage : INavigableView<ToggleFeatureCategoryViewModel>
{
    public ToggleFeatureCategoryViewModel ViewModel { get; }

    public ToggleFeaturesCategoryPage(ToggleFeatureCategoryViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }
}
