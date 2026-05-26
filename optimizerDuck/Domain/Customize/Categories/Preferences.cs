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

    [CustomizeSetting(Section = nameof(Sections.Taskbar), Icon = SymbolRegular.TextAlignDistributedEvenly24)]
    public class TaskbarAlignment : BaseCustomizeSetting
    {
        private const string RegPath =
            @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

        private const string RegName = "TaskbarAl";

        public override CustomizeControlType ControlType => CustomizeControlType.Dropdown;

        public override IReadOnlyList<SettingOption>? Options =>
            [
                Option("Center", 1),
                Option("Left", 0),
            ];

        public override object? CurrentValue =>
            RegistryService.Read<object>(new RegistryItem(RegPath, RegName));

        public override async Task ApplyAsync(object? value)
        {
            RegistryService.Write(new RegistryItem(RegPath, RegName, value ?? 1));
            await ExecutePostActionAsync();
        }

        protected override bool NeedsPostAction => true;
    }

    [CustomizeSetting(
        Section = nameof(Sections.Taskbar),
        Icon = SymbolRegular.Grid24,
        Recommendation = RecommendationState.Off
    )]
    public class TaskbarWidgets : BaseCustomizeSetting
    {
        protected override bool NeedsPostAction => true;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "TaskbarDa",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 1,
                },
                new()
                {
                    Path = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Feeds",
                    Name = "EnableFeeds",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 1,
                    IsOptional = true,
                },
                new()
                {
                    Path = @"HKLM\SOFTWARE\Policies\Microsoft\Dsh",
                    Name = "AllowNewsAndInterests",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 1,
                    IsOptional = true,
                },
            ];
    }

    [CustomizeSetting(Section = nameof(Sections.Taskbar), Icon = SymbolRegular.DesktopMac24)]
    public class TaskbarTaskViewButton : BaseCustomizeSetting
    {
        protected override bool NeedsPostAction => true;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "ShowTaskViewButton",
                    OnValue = 1,
                    OffValue = 0,
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
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path =
                        @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings",
                    Name = "TaskbarEndTask",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 0,
                },
            ];
    }

    [CustomizeSetting(Section = nameof(Sections.Appearance), Icon = SymbolRegular.DarkTheme24)]
    public class DarkMode : BaseCustomizeSetting
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    Name = "AppsUseLightTheme",
                    OnValue = 0,
                    OffValue = 1,
                    DefaultValue = 1,
                },
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    Name = "SystemUsesLightTheme",
                    OnValue = 0,
                    OffValue = 1,
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
        protected override bool NeedsPostAction => true;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "ShowSyncProviderNotifications",
                    OnValue = 1,
                    OffValue = 0,
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
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                    Name = "SystemPaneSuggestionsEnabled",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 1,
                },
            ];
    }

    [CustomizeSetting(Section = nameof(Sections.Explorer), Icon = SymbolRegular.Alert24)]
    public class ToastNotifications : BaseCustomizeSetting
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\PushNotifications",
                    Name = "ToastEnabled",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 1,
                },
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\PushNotifications",
                    Name = "LockScreenToastEnabled",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 1,
                    IsOptional = true,
                },
            ];
    }

    [CustomizeSetting(Section = nameof(Sections.Explorer), Icon = SymbolRegular.Table24)]
    public class ExplorerCompactMode : BaseCustomizeSetting
    {
        protected override bool NeedsPostAction => true;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "UseCompactMode",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 0,
                },
            ];
    }

    [CustomizeSetting(Section = nameof(Sections.Explorer), Icon = SymbolRegular.Grid24)]
    public class SnapAssistFlyout : BaseCustomizeSetting
    {
        protected override bool NeedsPostAction => true;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "EnableSnapAssistFlyout",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 1,
                },
            ];
    }

    [CustomizeSetting(Section = nameof(Sections.Explorer), Icon = SymbolRegular.CheckboxChecked24)]
    public class ExplorerItemCheckboxes : BaseCustomizeSetting
    {
        protected override bool NeedsPostAction => true;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "AutoCheckSelect",
                    OnValue = 1,
                    OffValue = 0,
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
        protected override bool NeedsPostAction => true;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "HideFileExt",
                    OnValue = 0,
                    OffValue = 1,
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
        protected override bool NeedsPostAction => true;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "Hidden",
                    OnValue = 1,
                    OffValue = 2,
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
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Clipboard",
                    Name = "EnableClipboardHistory",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 0,
                },
            ];
    }

    [CustomizeSetting(Section = nameof(Sections.Explorer), Icon = SymbolRegular.CursorClick24)]
    public class WindowShake : BaseCustomizeSetting
    {
        protected override bool NeedsPostAction => true;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "DisallowShaking",
                    OnValue = 0,
                    OffValue = 1,
                    DefaultValue = 0,
                },
            ];
    }

    [CustomizeSetting(Section = nameof(Sections.Taskbar), Icon = SymbolRegular.Clock24)]
    public class ShowSecondsInSystemClock : BaseCustomizeSetting
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "ShowSecondsInSystemClock",
                    OnValue = 1,
                    OffValue = 0,
                    DefaultValue = 0,
                },
            ];
    }

    [CustomizeSetting(Section = nameof(Sections.Explorer), Icon = SymbolRegular.Folder24)]
    public class LaunchToThisPc : BaseCustomizeSetting
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "LaunchTo",
                    OnValue = 1,
                    OffValue = 2,
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
        protected override bool NeedsPostAction => true;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Policies\Microsoft\Windows\Explorer",
                    Name = "DisableSearchBoxSuggestions",
                    OnValue = 0,
                    OffValue = 1,
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

        protected override bool NeedsPostAction => true;

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
}
