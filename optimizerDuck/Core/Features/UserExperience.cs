using System.Collections.ObjectModel;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.Features;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Services.Managers;
using optimizerDuck.UI.Views.Pages.Features;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.Features;

[FeatureCategory(PageType = typeof(UserExperienceFeatureCategory))]
public class UserExperience : IFeatureCategory
{
    public enum Sections
    {
        Taskbar,
        Appearance,
        SystemTray,
        Explorer
    }

    public string Name => Loc.Instance[$"ToggleFeature.{nameof(UserExperience)}"];
    public string Description => Loc.Instance[$"ToggleFeature.{nameof(UserExperience)}.Description"];
    public SymbolRegular Icon { get; init; } = SymbolRegular.Color24;
    public FeatureCategoryOrder Order { get; init; } = FeatureCategoryOrder.UserExperience;
    public ObservableCollection<IFeature> Features { get; init; } = [];

    [Feature(Section = nameof(Sections.Taskbar))]
    public class DisableTaskbarNewsAndInterests : BaseFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKLM\SOFTWARE\Policies\Microsoft\Dsh",
                Name = "AllowNewsAndInterests",
                OnValue = 0,
                OffValue = 1,
                DefaultValue = 1
            }
        ];
    }

    [Feature(Section = nameof(Sections.Appearance))]
    public class EnableDarkMode : BaseFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                Name = "AppsUseLightTheme",
                OnValue = 0,
                OffValue = 1,
                DefaultValue = 1
            }
        ];
    }

    [Feature(Section = nameof(Sections.Taskbar))]
    public class DisableVisualEffects : BaseFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                Name = "TaskbarAnimations",
                OnValue = 0,
                OffValue = 1,
                DefaultValue = 1
            }
        ];
    }

    [Feature(Section = nameof(Sections.SystemTray))]
    public class ShowSecondsInSystemClock : BaseFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                Name = "ShowSecondsInSystemClock",
                OnValue = 1,
                OffValue = 0,
                DefaultValue = 0
            }
        ];
    }

    [Feature(Section = nameof(Sections.Explorer))]
    public class EnableClassicContextMenu : BaseFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKCU\SOFTWARE\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32",
                Name = "",
                OnValue = 0,
                OffValue = 1,
                DefaultValue = 1,
                TreatMissingAsDefault = true
            }
        ];
    }
}