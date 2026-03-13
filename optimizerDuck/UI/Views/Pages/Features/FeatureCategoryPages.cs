using optimizerDuck.UI.ViewModels.Pages;

namespace optimizerDuck.UI.Views.Pages.Features;

public sealed class PreferencesFeatureCategory : FeatureCategoryPage
{
    public PreferencesFeatureCategory(FeatureCategoryViewModel viewModel) : base(viewModel)
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

public sealed class GamingFeatureCategory : FeatureCategoryPage
{
    public GamingFeatureCategory(FeatureCategoryViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }
}

public sealed class DesktopFeatureCategory : FeatureCategoryPage
{
    public DesktopFeatureCategory(FeatureCategoryViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }
}