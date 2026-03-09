using optimizerDuck.UI.ViewModels.Pages;

namespace optimizerDuck.UI.Views.Pages.Features;

public sealed class UserExperienceFeatureCategory : FeatureCategoryPage
{
    public UserExperienceFeatureCategory(FeatureCategoryViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }
}

public sealed class SystemFeatureCategory : FeatureCategoryPage
{
    public SystemFeatureCategory(FeatureCategoryViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }
}