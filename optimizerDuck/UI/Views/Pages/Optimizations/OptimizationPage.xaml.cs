using optimizerDuck.UI.ViewModels.Optimizer;
using Wpf.Ui.Abstractions.Controls;

namespace optimizerDuck.UI.Views.Pages.Optimizations;

public partial class OptimizationPage : INavigableView<OptimizationCategoryViewModel>
{
    public OptimizationPage(OptimizationCategoryViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }

    public OptimizationCategoryViewModel ViewModel { get; }
}