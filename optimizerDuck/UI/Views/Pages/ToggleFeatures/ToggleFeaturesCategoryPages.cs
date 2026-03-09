using optimizerDuck.UI.ViewModels.ToggleFeatures;

namespace optimizerDuck.UI.Views.Pages.ToggleFeatures;

public sealed class UserExperienceToggleFeaturesCategory : ToggleFeaturesCategoryPage
{
    public UserExperienceToggleFeaturesCategory(ToggleFeaturesCategoryViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }
}

public sealed class SystemToggleFeaturesCategory : ToggleFeaturesCategoryPage
{
    public SystemToggleFeaturesCategory(ToggleFeaturesCategoryViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }
}