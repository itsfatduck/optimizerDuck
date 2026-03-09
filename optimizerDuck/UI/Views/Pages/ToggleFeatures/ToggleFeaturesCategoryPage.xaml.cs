using System.Windows.Controls;
using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;
using ToggleFeaturesCategoryViewModel = optimizerDuck.UI.ViewModels.ToggleFeatures.ToggleFeaturesCategoryViewModel;

namespace optimizerDuck.UI.Views.Pages.ToggleFeatures;

public partial class ToggleFeaturesCategoryPage : INavigableView<ToggleFeaturesCategoryViewModel>
{
    public ToggleFeaturesCategoryViewModel ViewModel { get; }

    public ToggleFeaturesCategoryPage(ToggleFeaturesCategoryViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }
}
