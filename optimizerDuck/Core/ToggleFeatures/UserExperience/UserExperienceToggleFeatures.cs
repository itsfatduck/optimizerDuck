using System.Collections.ObjectModel;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Core.ToggleFeatures;
using optimizerDuck.Services.Managers;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.ToggleFeatures.UserExperience;

[ToggleFeatureCategory(PageType = typeof(UserExperienceToggleFeaturesCategory))]
public class UserExperience : IToggleFeatureCategory
{
    public string Name { get; init; } = Loc.Instance["ToggleFeature.Category.UserExperience.Name"];
    public string Description { get; init; } = Loc.Instance["ToggleFeature.Category.UserExperience.Description"];
    public SymbolRegular Icon { get; init; } = SymbolRegular.Color24;
    public ToggleFeatureCategoryOrder Order { get; init; } = ToggleFeatureCategoryOrder.UserExperience;
    public ObservableCollection<IToggleFeature> Features { get; init; } = [];

    [ToggleFeature]
    public class DisableTaskbarNewsAndInterests : SingleRegistryToggleFeature
    {
        public override RegistryToggle Toggle { get; } = new()
        {
            Path = @"HKLM\SOFTWARE\Policies\Microsoft\Dsh",
            Name = "AllowNewsAndInterests",
            OnValue = 0,
            OffValue = 1,
            DefaultValue = 1
        };
    }

    [ToggleFeature]
    public class EnableDarkMode : SingleRegistryToggleFeature
    {
        public override RegistryToggle Toggle { get; } = new()
        {
            Path = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            Name = "AppsUseLightTheme",
            OnValue = 0,
            OffValue = 1,
            DefaultValue = 1
        };
    }

    [ToggleFeature]
    public class DisableVisualEffects : SingleRegistryToggleFeature
    {
        public override RegistryToggle Toggle { get; } = new()
        {
            Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
            Name = "TaskbarAnimations",
            OnValue = 0,
            OffValue = 1,
            DefaultValue = 1
        };
    }

    [ToggleFeature]
    public class ShowSecondsInSystemClock : SingleRegistryToggleFeature
    {
        public override RegistryToggle Toggle { get; } = new()
        {
            Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
            Name = "ShowSecondsInSystemClock",
            OnValue = 1,
            OffValue = 0,
            DefaultValue = 0
        };
    }

    [ToggleFeature]
    public class EnableClassicContextMenu : SingleRegistryToggleFeature
    {
        public override RegistryToggle Toggle { get; } = new()
        {
            Path = @"HKCU\SOFTWARE\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32",
            Name = "",
            OnValue = 0,
            OffValue = 1,
            DefaultValue = 1,
            CheckKeyExists = true
        };
    }
}