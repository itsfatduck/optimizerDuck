using System.Collections.ObjectModel;
using optimizerDuck.Core.ToggleFeatures;
using optimizerDuck.Core.ToggleFeatures.AI;
using optimizerDuck.Core.ToggleFeatures.Privacy;
using optimizerDuck.Core.ToggleFeatures.System;
using optimizerDuck.Core.ToggleFeatures.UserExperience;
using optimizerDuck.UI.Views.Pages.ToggleFeatures;
using Wpf.Ui.Controls;
using SysToggle = optimizerDuck.Core.ToggleFeatures.System;

namespace optimizerDuck.Services;

public class ToggleFeaturesRegistry
{
    public ObservableCollection<ToggleFeatureCategory> Categories { get; } = [];

    public void RegisterCategories()
    {
        Categories.Add(new ToggleFeatureCategory
        {
            Name = "ToggleFeature.Category.AI.Name",
            Description = "ToggleFeature.Category.AI.Description",
            Icon = SymbolRegular.Brain24,
            PageType = typeof(AIToggleFeaturePage),
            Features =
            {
                new DisableWindowsCopilot(),
                new DisableBingInWindowsSearch(),
                new DisableSuggestionsInStart(),
                new DisableTailoredExperiences()
            }
        });

        Categories.Add(new ToggleFeatureCategory
        {
            Name = "ToggleFeature.Category.UserExperience.Name",
            Description = "ToggleFeature.Category.UserExperience.Description",
            Icon = SymbolRegular.Window24,
            PageType = typeof(UserExperienceToggleFeaturePage),
            Features =
            {
                new DisableTaskbarNewsAndInterests(),
                new EnableDarkMode(),
                new DisableVisualEffects(),
                new ShowSecondsInSystemClock(),
                new EnableClassicContextMenu()
            }
        });

        Categories.Add(new ToggleFeatureCategory
        {
            Name = "ToggleFeature.Category.Privacy.Name",
            Description = "ToggleFeature.Category.Privacy.Description",
            Icon = SymbolRegular.Shield24,
            PageType = typeof(PrivacyToggleFeaturePage),
            Features =
            {
                new DisableTelemetry(),
                new DisableDiagnosticData(),
                new DisableFeedbackNotifications()
            }
        });

        Categories.Add(new ToggleFeatureCategory
        {
            Name = "ToggleFeature.Category.System.Name",
            Description = "ToggleFeature.Category.System.Description",
            Icon = SymbolRegular.Settings24,
            PageType = typeof(SystemToggleFeaturePage),
            Features =
            {
                new SysToggle.DisableAutomaticWindowsUpdate(),
                new SysToggle.DisableStorageSense()
            }
        });
    }

    public ToggleFeatureCategory? GetCategoryByName(string name)
    {
        return Categories.FirstOrDefault(c => c.Name == name);
    }
}
