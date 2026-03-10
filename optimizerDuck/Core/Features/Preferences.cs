using System.Collections.ObjectModel;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.Features;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Services.Managers;
using optimizerDuck.Services.OptimizationServices;
using optimizerDuck.UI.Views.Pages.Features;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.Features;

[FeatureCategory(PageType = typeof(PreferencesFeatureCategory))]
public class Preferences : IFeatureCategory
{
    private enum Sections
    {
        Taskbar,
        Appearance,
        Explorer
    }

    public string Name => Loc.Instance[$"Features.{nameof(Preferences)}.Name"];
    public string Description => Loc.Instance[$"Features.{nameof(Preferences)}.Description"];
    public SymbolRegular Icon { get; init; } = SymbolRegular.Color24;
    public FeatureCategoryOrder Order { get; init; } = FeatureCategoryOrder.UserExperience;
    public ObservableCollection<IFeature> Features { get; init; } = [];

    [Feature(Section = nameof(Sections.Taskbar), Icon = SymbolRegular.News24)]
    public class TaskbarNewsAndInterests : BaseFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKLM\SOFTWARE\Policies\Microsoft\Dsh",
                Name = "AllowNewsAndInterests",
                OnValue = 1,
                OffValue = 0,
                DefaultValue = 1
            },
            new()
            {
                Path = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Feeds",
                Name = "EnableFeeds",
                OnValue = 1,
                OffValue = 0,
                DefaultValue = 1
            }
        ];
    }

    [Feature(Section = nameof(Sections.Taskbar), Icon = SymbolRegular.Search24)]
    public class TaskbarSearchBox : BaseFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Search",
                Name = "SearchboxTaskbarMode",
                OnValue = 1,
                OffValue = 0,
                DefaultValue = 1
            }
        ];
    }

    [Feature(Section = nameof(Sections.Taskbar), Icon = SymbolRegular.Globe24)]
    public class BingSearchInStartMenu : BaseFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Search",
                Name = "BingSearchEnabled",
                OnValue = 1,
                OffValue = 0,
                DefaultValue = 1
            }
        ];
    }

    [Feature(Section = nameof(Sections.Taskbar), Icon = SymbolRegular.Chat24)]
    public class TaskbarChatButton : BaseFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                Name = "TaskbarMn",
                OnValue = 1,
                OffValue = 0,
                DefaultValue = 1
            }
        ];
    }

    [Feature(Section = nameof(Sections.Taskbar), Icon = SymbolRegular.DesktopMac24)]
    public class TaskbarTaskViewButton : BaseFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                Name = "ShowTaskViewButton",
                OnValue = 1,
                OffValue = 0,
                DefaultValue = 1
            }
        ];
    }

    [Feature(Section = nameof(Sections.Taskbar), Icon = SymbolRegular.Video24)]
    public class MeetNow : BaseFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                Name = "HideSCAMeetNow",
                OnValue = 0,
                OffValue = 1,
                DefaultValue = 0
            }
        ];
    }

    [Feature(Section = nameof(Sections.Taskbar), Icon = SymbolRegular.People24)]
    public class TaskbarPeople : BaseFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced\People",
                Name = "PeopleBand",
                OnValue = 1,
                OffValue = 0,
                DefaultValue = 1
            }
        ];
    }

    [Feature(Section = nameof(Sections.Taskbar), Icon = SymbolRegular.WindowConsole20)]
    public class TaskbarEndTask : BaseFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings",
                Name = "TaskbarEndTask",
                OnValue = 1,
                OffValue = 0,
                DefaultValue = 0
            }
        ];
    }

    [Feature(Section = nameof(Sections.Appearance), Icon = SymbolRegular.DarkTheme24)]
    public class DarkMode : BaseFeature
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
            },
            new()
            {
                Path = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                Name = "SystemUsesLightTheme",
                OnValue = 0,
                OffValue = 1,
                DefaultValue = 1
            }
        ];
    }

    [Feature(Section = nameof(Sections.Explorer), Icon = SymbolRegular.AlertOff24)]
    public class ExplorerSyncNotifications : BaseFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                Name = "ShowSyncProviderNotifications",
                OnValue = 1,
                OffValue = 0,
                DefaultValue = 1
            }
        ];
    }

    [Feature(Section = nameof(Sections.Explorer), Icon = SymbolRegular.Lightbulb24)]
    public class SystemSuggestions : BaseFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                Name = "SystemPaneSuggestionsEnabled",
                OnValue = 1,
                OffValue = 0,
                DefaultValue = 1
            }
        ];
    }

    [Feature(Section = nameof(Sections.Explorer), Icon = SymbolRegular.Alert24)]
    public class ToastNotifications : BaseFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\PushNotifications",
                Name = "ToastEnabled",
                OnValue = 1,
                OffValue = 0,
                DefaultValue = 1
            },
            new()
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\PushNotifications",
                Name = "LockScreenToastEnabled",
                OnValue = 1,
                OffValue = 0,
                DefaultValue = 1
            }
        ];
    }

    [Feature(Section = nameof(Sections.Explorer), Icon = SymbolRegular.DocumentText24)]
    public class ShowFileExtensions : BaseFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                Name = "HideFileExt",
                OnValue = 0,
                OffValue = 1,
                DefaultValue = 1
            }
        ];
    }

    [Feature(Section = nameof(Sections.Explorer), Icon = SymbolRegular.FolderSearch24)]
    public class ShowHiddenFiles : BaseFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                Name = "Hidden",
                OnValue = 1,
                OffValue = 2,
                DefaultValue = 2
            }
        ];
    }

    [Feature(Section = nameof(Sections.Explorer), Icon = SymbolRegular.Clock24)]
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

    [Feature(Section = nameof(Sections.Explorer), Icon = SymbolRegular.CursorClick24)]
    public class ClassicContextMenu : BaseFeature
    {
        private const string BasePath =
            @"HKCU\SOFTWARE\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";

        private const string InprocPath =
            BasePath + @"\InprocServer32";

        public override Task<bool> GetStateAsync()
        {
            var exists = RegistryService.KeyExists(new RegistryItem(InprocPath));
            return Task.FromResult(exists);
        }

        public override Task EnableAsync()
        {
            RegistryService.CreateSubKey(new RegistryItem(InprocPath));
            return Task.CompletedTask;
        }

        public override Task DisableAsync()
        {
            RegistryService.DeleteSubKeyTree(new RegistryItem(BasePath));
            return Task.CompletedTask;
        }
    }
}