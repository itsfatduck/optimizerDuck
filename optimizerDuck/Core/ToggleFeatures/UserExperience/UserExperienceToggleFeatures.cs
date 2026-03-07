using System.Collections.ObjectModel;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Core.ToggleFeatures;
using optimizerDuck.Services.Managers;

namespace optimizerDuck.Core.ToggleFeatures.UserExperience;

[ToggleFeatureCategory]
public class UserExperience : IToggleFeatureCategory
{
    public string Name { get; init; } = Loc.Instance["ToggleFeature.Category.UserExperience.Name"];
    public ToggleFeatureCategoryOrder Order { get; init; } = ToggleFeatureCategoryOrder.UserExperience;
    public ObservableCollection<IToggleFeature> Features { get; init; } = [];

    [ToggleFeature(Id = "TF-UX-001", Risk = OptimizationRisk.Safe, Type = ToggleFeatureType.Registry)]
    public class DisableTaskbarNewsAndInterests : RegistryToggleFeature
    {
        public RegistryToggle Toggle { get; } = new()
        {
            Path = @"HKLM\SOFTWARE\Policies\Microsoft\Dsh",
            Name = "AllowNewsAndInterests",
            OnValue = 0,
            OffValue = 1,
            DefaultValue = 1
        };
    }

    [ToggleFeature(Id = "TF-UX-002", Risk = OptimizationRisk.Safe, Type = ToggleFeatureType.Registry)]
    public class EnableDarkMode : RegistryToggleFeature
    {
        public RegistryToggle Toggle { get; } = new()
        {
            Path = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            Name = "AppsUseLightTheme",
            OnValue = 0,
            OffValue = 1,
            DefaultValue = 1
        };
    }

    [ToggleFeature(Id = "TF-UX-003", Risk = OptimizationRisk.Safe, Type = ToggleFeatureType.Registry)]
    public class DisableVisualEffects : RegistryToggleFeature
    {
        public RegistryToggle Toggle { get; } = new()
        {
            Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
            Name = "TaskbarAnimations",
            OnValue = 0,
            OffValue = 1,
            DefaultValue = 1
        };
    }

    [ToggleFeature(Id = "TF-UX-004", Risk = OptimizationRisk.Safe, Type = ToggleFeatureType.Registry)]
    public class ShowSecondsInSystemClock : RegistryToggleFeature
    {
        public RegistryToggle Toggle { get; } = new()
        {
            Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
            Name = "ShowSecondsInSystemClock",
            OnValue = 1,
            OffValue = 0,
            DefaultValue = 0
        };
    }

    [ToggleFeature(Id = "TF-UX-005", Risk = OptimizationRisk.Safe, Type = ToggleFeatureType.Registry)]
    public class EnableClassicContextMenu : RegistryToggleFeature
    {
        public RegistryToggle Toggle { get; } = new()
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
