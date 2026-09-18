#if DEBUG
using optimizerDuck.UI.Pages.Optimizations;
using optimizerDuck.UI.ViewModels.Optimizer;

namespace optimizerDuck.UI.Pages;

/// <summary>
///     Provides the page that shows the debug category.
/// </summary>
public sealed class DebugOptimizerPage : OptimizationPage
{
    public DebugOptimizerPage(OptimizationCategoryViewModel viewModel)
        : base(viewModel)
    {
        InitializeComponent();
    }
}
#endif
