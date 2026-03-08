using optimizerDuck.UI.ViewModels.Optimizer;
using optimizerDuck.UI.ViewModels.Pages;
using optimizerDuck.UI.Views.Pages.Optimizations;
using optimizerDuck.UI.Views.Pages.ToggleFeatures;

public sealed class UserExperienceToggleFeaturesCategory : ToggleFeaturesCategoryPage
{
    public UserExperienceToggleFeaturesCategory(ToggleFeatureCategoryViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }
}

public sealed class SystemToggleFeaturesCategory : ToggleFeaturesCategoryPage
{
    public SystemToggleFeaturesCategory(ToggleFeatureCategoryViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }
}