#if DEBUG
using optimizerDuck.UI.Pages.Optimizations;
using optimizerDuck.UI.ViewModels.Optimizer;

namespace optimizerDuck.UI.Pages;

/// <summary>
///     Provides the page that shows the debug category.
/// </summary>
public sealed class DebugOptimizerPage(OptimizationCategoryViewModel viewModel)
    : OptimizationPage(viewModel);
#endif
