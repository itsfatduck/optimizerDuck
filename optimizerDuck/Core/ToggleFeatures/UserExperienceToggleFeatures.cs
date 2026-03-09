using System.Collections.ObjectModel;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.ToggleFeatures;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Services.Managers;
using optimizerDuck.UI.Views.Pages.ToggleFeatures;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.ToggleFeatures;

[ToggleFeatureCategory(PageType = typeof(UserExperienceToggleFeaturesCategory))]
public class UserExperience : IToggleFeaturesCategory
{
    public string Name => Loc.Instance[$"ToggleFeature.Category.{nameof(UserExperience)}"];
    public string Description => Loc.Instance[$"ToggleFeature.Category.{nameof(UserExperience)}.Description"];
    public SymbolRegular Icon { get; init; } = SymbolRegular.Color24;
    public ToggleFeaturesCategoryOrder Order { get; init; } = ToggleFeaturesCategoryOrder.UserExperience;
    public ObservableCollection<IToggleFeature> Features { get; init; } = [];

    public enum Sections
    {
        Taskbar,
        Appearance,
        SystemTray,
        Explorer
    }

    [ToggleFeature(Section = nameof(Sections.Taskbar))]
    public class DisableTaskbarNewsAndInterests : BaseToggleFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new RegistryToggle
            {
                Path = @"HKLM\SOFTWARE\Policies\Microsoft\Dsh",
                Name = "AllowNewsAndInterests",
                OnValue = 0,
                OffValue = 1,
                DefaultValue = 1
            }
        ];
    }

    [ToggleFeature(Section = nameof(Sections.Appearance))]
    public class EnableDarkMode : BaseToggleFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new RegistryToggle
            {
                Path = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                Name = "AppsUseLightTheme",
                OnValue = 0,
                OffValue = 1,
                DefaultValue = 1
            }
        ];
    }

    [ToggleFeature(Section = nameof(Sections.Taskbar))]
    public class DisableVisualEffects : BaseToggleFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new RegistryToggle
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                Name = "TaskbarAnimations",
                OnValue = 0,
                OffValue = 1,
                DefaultValue = 1
            }
        ];
    }

    [ToggleFeature(Section = nameof(Sections.SystemTray))]
    public class ShowSecondsInSystemClock : BaseToggleFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new RegistryToggle
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                Name = "ShowSecondsInSystemClock",
                OnValue = 1,
                OffValue = 0,
                DefaultValue = 0
            }
        ];
    }

    [ToggleFeature(Section = nameof(Sections.Explorer))]
    public class EnableClassicContextMenu : BaseToggleFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new RegistryToggle
            {
                Path = @"HKCU\SOFTWARE\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32",
                Name = "",
                OnValue = 0,
                OffValue = 1,
                DefaultValue = 1,
                CheckKeyExists = true
            }
        ];
    }
}
