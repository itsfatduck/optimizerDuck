using optimizerDuck.UI.Pages.Optimizations;
using optimizerDuck.UI.ViewModels.Optimizer;

namespace optimizerDuck.UI.Pages.Optimize.Categories;

public sealed class PowerManagementOptimizerPage(OptimizationCategoryViewModel viewModel)
    : OptimizationPage(viewModel);

public sealed class UserExperienceOptimizerPage(OptimizationCategoryViewModel viewModel)
    : OptimizationPage(viewModel);

public sealed class BloatwareAndServicesOptimizerPage(OptimizationCategoryViewModel viewModel)
    : OptimizationPage(viewModel);

public sealed class GpuOptimizerPage(OptimizationCategoryViewModel viewModel)
    : OptimizationPage(viewModel);

public sealed class PerformanceOptimizerPage(OptimizationCategoryViewModel viewModel)
    : OptimizationPage(viewModel);

public sealed class SecurityAndPrivacyOptimizerPage(OptimizationCategoryViewModel viewModel)
    : OptimizationPage(viewModel);

public sealed class AIOptimizerPage(OptimizationCategoryViewModel viewModel)
    : OptimizationPage(viewModel);
