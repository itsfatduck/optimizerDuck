using System.Collections.ObjectModel;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Customize.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.Optimization.Providers;
using optimizerDuck.UI.Pages.Customize.Categories;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Customize.Categories;

[CustomizeCategory(PageType = typeof(PreferencesFeatureCategory))]
public class Preferences : ICustomizeCategory
{
    private enum Sections
    {
        Taskbar,
        Appearance,
        Explorer,
    }

    public string Name => Loc.Instance[$"Customize.{nameof(Preferences)}.Name"];
    public string Description => Loc.Instance[$"Customize.{nameof(Preferences)}.Description"];
    public SymbolRegular Icon { get; init; } = SymbolRegular.Color24;
    public CustomizeOrder Order { get; init; } = CustomizeOrder.UserExperience;
    public ObservableCollection<ICustomizeSetting> Features { get; init; } = [];

    [CustomizeSetting(
        Section = nameof(Sections.Taskbar),
        Icon = SymbolRegular.TextAlignDistributedEvenly24
    )]
    public class TaskbarAlignment : BaseCustomizeSetting
    {
        private const string RegPath =
            @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

        private const string RegName = "TaskbarAl";

        public override CustomizeControlType ControlType => CustomizeControlType.Dropdown;

        protected override IReadOnlyList<SettingOption>? GetOptions() =>
            [Option("Center", RegPath, RegName, 1), Option("Left", RegPath, RegName, 0)];

        protected override CustomizeRefreshScope RefreshScope =>
            CustomizeRefreshScope.TaskbarSettings;
    }

    [CustomizeSetting(
        Section = nameof(Sections.Taskbar),
        Icon = SymbolRegular.Grid24,
        Recommendation = RecommendationState.Off
    )]
    public class TaskbarWidgets : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope =>
            CustomizeRefreshScope.TaskbarSettings;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "TaskbarDa",
                    OnValues = [1],
                    OffValues = [0],
                    DefaultValue = 1,
                },
                new()
                {
                    Path = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Feeds",
                    Name = "EnableFeeds",
                    OnValues = [1],
                    OffValues = [0],
                    DefaultValue = 1,
                    IsOptional = true,
                },
                new()
                {
                    Path = @"HKLM\SOFTWARE\Policies\Microsoft\Dsh",
                    Name = "AllowNewsAndInterests",
                    OnValues = [1],
                    OffValues = [0],
                    DefaultValue = 1,
                    IsOptional = true,
                },
            ];
    }

    [CustomizeSetting(Section = nameof(Sections.Taskbar), Icon = SymbolRegular.DesktopMac24)]
    public class TaskbarTaskViewButton : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope =>
            CustomizeRefreshScope.TaskbarSettings;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "ShowTaskViewButton",
                    OnValues = [1],
                    OffValues = [0],
                    DefaultValue = 1,
                },
            ];
    }

    [CustomizeSetting(
        Section = nameof(Sections.Taskbar),
        Icon = SymbolRegular.WindowConsole20,
        Recommendation = RecommendationState.On
    )]
    public class TaskbarEndTask : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope =>
            CustomizeRefreshScope.TaskbarSettings;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path =
                        @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings",
                    Name = "TaskbarEndTask",
                    OnValues = [1],
                    OffValues = [0],
                    DefaultValue = 0,
                },
            ];
    }

    [CustomizeSetting(Section = nameof(Sections.Appearance), Icon = SymbolRegular.DarkTheme24)]
    public class DarkMode : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope =>
            CustomizeRefreshScope.Settings | CustomizeRefreshScope.Theme;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    Name = "AppsUseLightTheme",
                    OnValues = [0],
                    OffValues = [1],
                    DefaultValue = 1,
                },
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    Name = "SystemUsesLightTheme",
                    OnValues = [0],
                    OffValues = [1],
                    DefaultValue = 1,
                },
            ];
    }

    [CustomizeSetting(
        Section = nameof(Sections.Explorer),
        Icon = SymbolRegular.AlertOff24,
        Recommendation = RecommendationState.Off
    )]
    public class ExplorerSyncNotifications : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Default;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "ShowSyncProviderNotifications",
                    OnValues = [1],
                    OffValues = [0],
                    DefaultValue = 1,
                },
            ];
    }

    [CustomizeSetting(
        Section = nameof(Sections.Explorer),
        Icon = SymbolRegular.Lightbulb24,
        Recommendation = RecommendationState.Off
    )]
    public class SystemSuggestions : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Settings;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                    Name = "SystemPaneSuggestionsEnabled",
                    OnValues = [1],
                    OffValues = [0],
                    DefaultValue = 1,
                },
            ];
    }

    [CustomizeSetting(Section = nameof(Sections.Explorer), Icon = SymbolRegular.Alert24)]
    public class ToastNotifications : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Settings;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\PushNotifications",
                    Name = "ToastEnabled",
                    OnValues = [1],
                    OffValues = [0],
                    DefaultValue = 1,
                },
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\PushNotifications",
                    Name = "LockScreenToastEnabled",
                    OnValues = [1],
                    OffValues = [0],
                    DefaultValue = 1,
                    IsOptional = true,
                },
            ];
    }

    [CustomizeSetting(Section = nameof(Sections.Explorer), Icon = SymbolRegular.Table24)]
    public class ExplorerCompactMode : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Default;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "UseCompactMode",
                    OnValues = [1],
                    OffValues = [0],
                    DefaultValue = 0,
                },
            ];
    }

    [CustomizeSetting(Section = nameof(Sections.Explorer), Icon = SymbolRegular.Grid24)]
    public class SnapAssistFlyout : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Default;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "EnableSnapAssistFlyout",
                    OnValues = [1],
                    OffValues = [0],
                    DefaultValue = 1,
                },
            ];
    }

    [CustomizeSetting(Section = nameof(Sections.Explorer), Icon = SymbolRegular.CheckboxChecked24)]
    public class ExplorerItemCheckboxes : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Default;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "AutoCheckSelect",
                    OnValues = [1],
                    OffValues = [0],
                    DefaultValue = 0,
                },
            ];
    }

    [CustomizeSetting(
        Section = nameof(Sections.Explorer),
        Icon = SymbolRegular.DocumentText24,
        Recommendation = RecommendationState.On
    )]
    public class ShowFileExtensions : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.ExplorerView;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "HideFileExt",
                    OnValues = [0],
                    OffValues = [1],
                    DefaultValue = 1,
                },
            ];
    }

    [CustomizeSetting(
        Section = nameof(Sections.Explorer),
        Icon = SymbolRegular.FolderProhibited48,
        Recommendation = RecommendationState.On
    )]
    public class ShowHiddenFiles : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.ExplorerView;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "Hidden",
                    OnValues = [1],
                    OffValues = [2],
                    DefaultValue = 2,
                },
            ];
    }

    [CustomizeSetting(
        Section = nameof(Sections.Explorer),
        Icon = SymbolRegular.DocumentText24,
        Recommendation = RecommendationState.On
    )]
    public class ClipboardHistory : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Settings;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Clipboard",
                    Name = "EnableClipboardHistory",
                    OnValues = [1],
                    OffValues = [0],
                    DefaultValue = 0,
                },
            ];
    }

    [CustomizeSetting(Section = nameof(Sections.Explorer), Icon = SymbolRegular.CursorClick24)]
    public class WindowShake : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Default;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "DisallowShaking",
                    OnValues = [0],
                    OffValues = [1],
                    DefaultValue = 0,
                },
            ];
    }

    [CustomizeSetting(Section = nameof(Sections.Taskbar), Icon = SymbolRegular.Clock24)]
    public class ShowSecondsInSystemClock : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope =>
            CustomizeRefreshScope.TaskbarSettings;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "ShowSecondsInSystemClock",
                    OnValues = [1],
                    OffValues = [0],
                    DefaultValue = 0,
                },
            ];
    }

    [CustomizeSetting(Section = nameof(Sections.Explorer), Icon = SymbolRegular.Folder24)]
    public class LaunchToThisPc : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Default;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "LaunchTo",
                    OnValues = [1],
                    OffValues = [2],
                    DefaultValue = 2,
                },
            ];
    }

    [CustomizeSetting(
        Section = nameof(Sections.Taskbar),
        Icon = SymbolRegular.Search24,
        Recommendation = RecommendationState.Off
    )]
    public class BingSearch : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope =>
            CustomizeRefreshScope.TaskbarSettings;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Policies\Microsoft\Windows\Explorer",
                    Name = "DisableSearchBoxSuggestions",
                    OnValues = [0],
                    OffValues = [1],
                    DefaultValue = 0,
                },
            ];
    }

    [CustomizeSetting(
        Section = nameof(Sections.Explorer),
        Icon = SymbolRegular.CursorClick24,
        Recommendation = RecommendationState.On
    )]
    public class ClassicContextMenu : BaseCustomizeSetting
    {
        private const string BasePath =
            @"HKCU\SOFTWARE\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";

        private const string InprocPath = BasePath + @"\InprocServer32";

        protected override CustomizeRefreshScope RefreshScope =>
            CustomizeRefreshScope.Default | CustomizeRefreshScope.PolicyUpdate;

        protected override IReadOnlyList<string> GetWatchedRegistryPaths() => [BasePath];

        public override Task<bool> GetStateAsync()
        {
            var exists = RegistryService.KeyExists(new RegistryItem(InprocPath));
            return Task.FromResult(exists);
        }

        public override async Task ApplyAsync(object? value)
        {
            var isOn = value is bool b && b;

            if (isOn)
                RegistryService.Write(new RegistryItem(InprocPath, null, ""));
            else
                RegistryService.DeleteSubKeyTree(new RegistryItem(BasePath));

            if (NeedsPostAction)
                await ExecutePostActionAsync();
        }
    }

    [CustomizeSetting(Section = nameof(Sections.Taskbar), Icon = SymbolRegular.Search24)]
    public class SearchBoxTaskbarMode : BaseCustomizeSetting
    {
        private const string RegPath = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Search";
        private const string RegName = "SearchboxTaskbarMode";

        public override CustomizeControlType ControlType => CustomizeControlType.Dropdown;

        protected override IReadOnlyList<SettingOption>? GetOptions()
        {
            if (Shared.IsWindows11OrGreater)
                return
                [
                    Option("Hidden", RegPath, RegName, 0),
                    Option("Icon", RegPath, RegName, 1),
                    Option("IconAndLabel", RegPath, RegName, 2),
                    Option("SearchBox", RegPath, RegName, 3),
                ];
            return
            [
                Option("Hidden", RegPath, RegName, 0),
                Option("Icon", RegPath, RegName, 1),
                Option("SearchBox", RegPath, RegName, 2),
            ];
        }

        protected override CustomizeRefreshScope RefreshScope =>
            CustomizeRefreshScope.TaskbarSettings;
    }

    [CustomizeSetting(Section = nameof(Sections.Taskbar), Icon = SymbolRegular.CursorClick24)]
    public class TaskbarLastActiveClick : BaseCustomizeSetting
    {
        private const string RegPath =
            @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        private const string RegName = "LastActiveClick";

        protected override CustomizeRefreshScope RefreshScope =>
            CustomizeRefreshScope.TaskbarSettings;

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
