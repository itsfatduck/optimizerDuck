using System.Collections.ObjectModel;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Features.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Managers;
using optimizerDuck.Services.OptimizationServices;
using optimizerDuck.UI.Pages.Features;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Features.Categories;

[FeatureCategory(PageType = typeof(GamingFeatureCategory))]
public class Gaming : IFeatureCategory
{
    private enum Sections
    {
        GameSettings,
        Input,
        Display,
    }

    public string Name => Loc.Instance[$"Features.{nameof(Gaming)}.Name"];
    public string Description => Loc.Instance[$"Features.{nameof(Gaming)}.Description"];
    public SymbolRegular Icon { get; init; } = SymbolRegular.XboxController24;
    public FeatureCategoryOrder Order { get; init; } = FeatureCategoryOrder.Gaming;
    public ObservableCollection<IFeature> Features { get; init; } = [];

    [Feature(Section = nameof(Sections.GameSettings), Icon = SymbolRegular.XboxController24)]
    public class GameMode : BaseFeature
    {
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

    [Feature(Section = nameof(Sections.GameSettings), Icon = SymbolRegular.XboxConsole24)]
    public class GameBar : BaseFeature
    {
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
                },
                new()
                {
                    Path = @"HKCU\Software\Microsoft\GameBar",
                    Name = "UseNexusForGameBarEnabled",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 1,
                },
                new()
                {
                    Path = @"HKCU\Software\Microsoft\GameBar",
                    Name = "GamePanelStartupTipIndex",
                    OnValue = 3,
                    OffValue = 0,
                    DefaultValue = 3,
                },
            ];
    }

    [Feature(Section = nameof(Sections.GameSettings), Icon = SymbolRegular.Record24)]
    public class BackgroundRecording : BaseFeature
    {
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

    [Feature(Section = nameof(Sections.Input), Icon = SymbolRegular.Cursor24)]
    public class MouseAcceleration : BaseFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Control Panel\Mouse",
                    Name = "MouseSpeed",
                    OnValue = "1",
                    OffValue = "0",
                    DefaultValue = "1",
                },
                new()
                {
                    Path = @"HKCU\Control Panel\Mouse",
                    Name = "MouseThreshold1",
                    OnValue = "6",
                    OffValue = "0",
                    DefaultValue = "6",
                },
                new()
                {
                    Path = @"HKCU\Control Panel\Mouse",
                    Name = "MouseThreshold2",
                    OnValue = "10",
                    OffValue = "0",
                    DefaultValue = "10",
                },
            ];
    }

    [Feature(Section = nameof(Sections.Display), Icon = SymbolRegular.FullScreenMaximize24)]
    public class FullscreenOptimizations : BaseFeature
    {
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

    [Feature(Section = nameof(Sections.Display), Icon = SymbolRegular.VideoClip24)]
    public class HardwareAcceleratedGpuScheduling : BaseFeature
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
