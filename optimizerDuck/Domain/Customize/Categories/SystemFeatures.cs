using System.Collections.ObjectModel;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Customize.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Managers;
using optimizerDuck.Services.Optimization.Providers;
using optimizerDuck.UI.Pages.Customize;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Customize.Categories;

[CustomizeCategory(PageType = typeof(SystemFeatureCategory))]
public class SystemFeatures : ICustomizeCategory
{
    private enum Sections
    {
        Input,
        Power,
        Developer,
    }

    public string Name => Loc.Instance[$"Customize.{nameof(SystemFeatures)}.Name"];
    public string Description => Loc.Instance[$"Customize.{nameof(SystemFeatures)}.Description"];
    public SymbolRegular Icon { get; init; } = SymbolRegular.WindowSettings20;
    public CustomizeOrder Order { get; init; } = CustomizeOrder.System;
    public ObservableCollection<ICustomizeSetting> Features { get; init; } = [];

    [CustomizeSetting(
        Section = nameof(Sections.Input),
        Icon = SymbolRegular.NumberSymbol24,
        Recommendation = RecommendationState.On
    )]
    public class NumLockOnBoot : BaseCustomizeSetting
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKU\.DEFAULT\Control Panel\Keyboard",
                    Name = "InitialKeyboardIndicators",
                    OnValue = 2,
                    OffValue = 0,
                    DefaultValue = 0,
                },
                new()
                {
                    Path = @"HKCU\Control Panel\Keyboard",
                    Name = "InitialKeyboardIndicators",
                    OnValue = 2,
                    OffValue = 0,
                    DefaultValue = 0,
                },
            ];
    }



    #region Developer

    [CustomizeSetting(
        Section = nameof(Sections.Developer),
        Icon = SymbolRegular.DeveloperBoard24,
        Recommendation = RecommendationState.Depends
    )]
    public class DeveloperMode : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Settings;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock",
                    Name = "AllowDevelopmentWithoutDevLicense",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 0,
                },
            ];
    }

    [CustomizeSetting(
        Section = nameof(Sections.Developer),
        Icon = SymbolRegular.Shield24,
        Recommendation = RecommendationState.Depends
    )]
    public class AllowAllTrustedApps : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Settings;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock",
                    Name = "AllowAllTrustedApps",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 0,
                },
            ];
    }

    [CustomizeSetting(
        Section = nameof(Sections.Developer),
        Icon = SymbolRegular.Folder24,
        Recommendation = RecommendationState.On
    )]
    public class LongPathsEnabled : BaseCustomizeSetting
    {
        private const string Path = @"HKLM\SYSTEM\CurrentControlSet\Control\FileSystem";

        protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Settings;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = Path,
                    Name = "LongPathsEnabled",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 0,
                },
            ];
    }

    #endregion

[CustomizeSetting(Section = nameof(Sections.Power), Icon = SymbolRegular.BatteryCharge24)]
    public class ShowBatteryPercentage : BaseCustomizeSetting
    {
        private const string RegPath =
            @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        private const string RegName = "IsBatteryPercentageEnabled";

        protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Settings;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = RegPath,
                    Name = RegName,
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 0,
                },
            ];
    }
}
