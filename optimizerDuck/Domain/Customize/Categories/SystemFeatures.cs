using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Win32;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Conditions;
using optimizerDuck.Domain.Customize.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.Optimization.Providers;
using optimizerDuck.UI.Pages.Customize.Categories;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Customize.Categories;

[CustomizeCategory(PageType = typeof(SystemFeatureCategory))]
public class SystemFeatures : LocalizedObject, ICustomizeCategory
{
    private enum Sections
    {
        Input,
        Power,
        Developer,
        Boot,
        Network,
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
        private const string PathDefault = @"HKU\.DEFAULT\Control Panel\Keyboard";
        private const string PathCurrent = @"HKCU\Control Panel\Keyboard";
        private const string ValueName = "InitialKeyboardIndicators";

        protected override IReadOnlyList<string> GetWatchedRegistryPaths() => [PathDefault, PathCurrent];

        public override Task<bool> GetStateAsync()
        {
            return Task.Run(() =>
            {
                bool IsNumLockOn(string path)
                {
                    var raw = RegistryService.Read<object?>(new RegistryItem(path, ValueName));
                    if (raw == null)
                        return false;
                    if (long.TryParse(raw.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                        return (v & 2) == 2;
                    return false;
                }

                return IsNumLockOn(PathDefault) && IsNumLockOn(PathCurrent);
            });
        }

        public override async Task ApplyAsync(object? value)
        {
            var isOn = value is bool b && b;

            var currentRaw = RegistryService.Read<object?>(new RegistryItem(PathCurrent, ValueName));
            var defaultRaw = RegistryService.Read<object?>(new RegistryItem(PathDefault, ValueName));

            RegistryService.Write(
                new RegistryItem(PathCurrent, ValueName, SetNumLockBit(currentRaw, isOn), RegistryValueKind.String));
            RegistryService.Write(
                new RegistryItem(PathDefault, ValueName, SetNumLockBit(defaultRaw, isOn), RegistryValueKind.String));

            if (NeedsPostAction)
                await ExecutePostActionAsync();
        }

        private static string SetNumLockBit(object? raw, bool enabled)
        {
            if (!long.TryParse(raw?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                value = 0;
            }

            value = enabled ? value | 2 : value & ~2;

            return value.ToString(CultureInfo.InvariantCulture);
        }
    }

    #region Boot

    [CustomizeSetting(Section = nameof(Sections.Boot), Icon = SymbolRegular.Info24)]
    public class VerboseStatus : BaseCustomizeSetting
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                    Name = "VerboseStatus",
                    OnValues = [1],
                    OffValues = [0],
                    DefaultValue = 0,
                },
            ];
    }

    [CustomizeSetting(
        Section = nameof(Sections.Boot),
        Icon = SymbolRegular.Clock24,
        Recommendation = RecommendationState.Depends
    )]
    public class UtcHardwareClock : BaseCustomizeSetting
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKLM\SYSTEM\CurrentControlSet\Control\TimeZoneInformation",
                    Name = "RealTimeIsUniversal",
                    OnValues = [1],
                    OffValues = [0],
                    DefaultValue = 0,
                },
            ];
    }

    #endregion

    #region Network

    [CustomizeSetting(Section = nameof(Sections.Network), Icon = SymbolRegular.Globe24)]
    public class DisableSmartNameResolution : BaseCustomizeSetting
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKLM\SOFTWARE\Policies\Microsoft\Windows NT\DNSClient",
                    Name = "DisableSmartNameResolution",
                    OnValues = [1],
                    OffValues = [0],
                    DefaultValue = 0,
                },
            ];
    }

    #endregion

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
                    OnValues = [1],
                    OffValues = [0],
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
                    OnValues = [1],
                    OffValues = [0],
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
                    OnValues = [1],
                    OffValues = [0],
                    DefaultValue = 0,
                },
            ];
    }

    #endregion

    [CustomizeSetting(
        Section = nameof(Sections.Power),
        Icon = SymbolRegular.BatteryCharge24,
        Condition = typeof(Windows11Condition)
    )]
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
                    OnValues = [1],
                    OffValues = [0],
                    DefaultValue = 0,
                },
            ];
    }
}
