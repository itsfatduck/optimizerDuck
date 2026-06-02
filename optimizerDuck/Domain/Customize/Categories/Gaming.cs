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

[CustomizeCategory(PageType = typeof(GamingFeatureCategory))]
public class Gaming : ICustomizeCategory
{
    private enum Sections
    {
        GameSettings,
        Input,
        Display,
    }

    public string Name => Loc.Instance[$"Customize.{nameof(Gaming)}.Name"];
    public string Description => Loc.Instance[$"Customize.{nameof(Gaming)}.Description"];
    public SymbolRegular Icon { get; init; } = SymbolRegular.XboxController24;
    public CustomizeOrder Order { get; init; } = CustomizeOrder.Gaming;
    public ObservableCollection<ICustomizeSetting> Features { get; init; } = [];

    [CustomizeSetting(
        Section = nameof(Sections.GameSettings),
        Icon = SymbolRegular.XboxController24,
        Recommendation = RecommendationState.On
    )]
    public class GameMode : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Settings;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\GameBar",
                    Name = "AllowAutoGameMode",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 1,
                },
                new()
                {
                    Path = @"HKCU\Software\Microsoft\GameBar",
                    Name = "AutoGameModeEnabled",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 1,
                },
            ];
    }

    [CustomizeSetting(
        Section = nameof(Sections.GameSettings),
        Icon = SymbolRegular.XboxConsole24,
        Recommendation = RecommendationState.Off
    )]
    public class GameBar : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Settings;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\System\GameConfigStore",
                    Name = "GameBarEnabled",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 1,
                },
                new()
                {
                    Path = @"HKCU\Software\Microsoft\GameBar",
                    Name = "ShowStartupPanel",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 1,
                    IsOptional = true,
                },
                new()
                {
                    Path = @"HKCU\Software\Microsoft\GameBar",
                    Name = "UseNexusForGameBarEnabled",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 1,
                    IsOptional = true,
                },
                new()
                {
                    Path = @"HKCU\Software\Microsoft\GameBar",
                    Name = "GamePanelStartupTipIndex",
                    OnValue = 3,
                    OffValue = 0,
                    DefaultValue = 3,
                    IsOptional = true,
                },
            ];
    }

    [CustomizeSetting(
        Section = nameof(Sections.GameSettings),
        Icon = SymbolRegular.Record24,
        Recommendation = RecommendationState.Off
    )]
    public class BackgroundRecording : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope =>
            CustomizeRefreshScope.Settings | CustomizeRefreshScope.PolicyUpdate;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR",
                    Name = "AppCaptureEnabled",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 1,
                },
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR",
                    Name = "HistoricalCaptureEnabled",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 0,
                    IsOptional = true,
                },
                new()
                {
                    Path = @"HKCU\System\GameConfigStore",
                    Name = "GameDVR_Enabled",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 1,
                },
                new()
                {
                    Path = @"HKCU\System\GameConfigStore",
                    Name = "GameDVR_HistoricalCaptureEnabled",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 0,
                    IsOptional = true,
                },
                new()
                {
                    Path = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\GameDVR",
                    Name = "AllowGameDVR",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 1,
                },
            ];
    }

    [CustomizeSetting(
        Section = nameof(Sections.Input),
        Icon = SymbolRegular.Cursor24,
        Recommendation = RecommendationState.Off
    )]
    public class MouseAcceleration : BaseCustomizeSetting
    {
        private const string Path = @"HKCU\Control Panel\Mouse";

        protected override CustomizeRefreshScope RefreshScope =>
            CustomizeRefreshScope.PolicyUpdate;

        protected override IReadOnlyList<string> GetWatchedRegistryPaths() => [Path];

        public override Task<bool> GetStateAsync()
        {
            return Task.Run(() =>
            {
                // Mouse acceleration is ON if any of the values are non-zero
                var mouseSpeed = RegistryService.Read<string>(new RegistryItem(Path, "MouseSpeed"));
                var threshold1 = RegistryService.Read<string>(
                    new RegistryItem(Path, "MouseThreshold1")
                );
                var threshold2 = RegistryService.Read<string>(
                    new RegistryItem(Path, "MouseThreshold2")
                );

                // Check if any value is non-zero (acceleration enabled)
                var isNonZero =
                    (int.TryParse(mouseSpeed, out var speed) && speed != 0)
                    || (int.TryParse(threshold1, out var t1) && t1 != 0)
                    || (int.TryParse(threshold2, out var t2) && t2 != 0);

                return isNonZero;
            });
        }

        public override async Task ApplyAsync(object? value)
        {
            var isOn = value is bool b && b;

            RegistryService.Write(new RegistryItem(Path, "MouseSpeed", isOn ? "1" : "0"));
            RegistryService.Write(new RegistryItem(Path, "MouseThreshold1", isOn ? "6" : "0"));
            RegistryService.Write(new RegistryItem(Path, "MouseThreshold2", isOn ? "10" : "0"));

            if (NeedsPostAction)
                await ExecutePostActionAsync();
        }
    }

    [CustomizeSetting(
        Section = nameof(Sections.Display),
        Icon = SymbolRegular.FullScreenMaximize24,
        Recommendation = RecommendationState.Depends
    )]
    public class FullscreenOptimizations : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Settings;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\System\GameConfigStore",
                    Name = "GameDVR_DXGIHonorFSEWindowsCompatible",
                    OnValue = 0,
                    OffValue = 1,
                    DefaultValue = 0,
                },
                new()
                {
                    Path = @"HKCU\System\GameConfigStore",
                    Name = "GameDVR_FSEBehavior",
                    OnValue = 0,
                    OffValue = 2,
                    DefaultValue = 0,
                },
                new()
                {
                    Path = @"HKCU\System\GameConfigStore",
                    Name = "GameDVR_FSEBehaviorMode",
                    OnValue = 0,
                    OffValue = 2,
                    DefaultValue = 0,
                },
                new()
                {
                    Path = @"HKCU\System\GameConfigStore",
                    Name = "GameDVR_HonorUserFSEBehaviorMode",
                    OnValue = 0,
                    OffValue = 1,
                    DefaultValue = 0,
                },
            ];
    }

    [CustomizeSetting(
        Section = nameof(Sections.Display),
        Icon = SymbolRegular.VideoClip24,
        Recommendation = RecommendationState.On
    )]
    public class HardwareAcceleratedGpuScheduling : BaseCustomizeSetting
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
                    Name = "HwSchMode",
                    OnValue = 2,
                    OffValue = 1,
                    DefaultValue = 1,
                },
            ];
    }
}
