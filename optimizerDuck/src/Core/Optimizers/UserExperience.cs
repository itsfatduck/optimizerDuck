using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using optimizerDuck.Core.Helpers;
using optimizerDuck.Core.Services;
using optimizerDuck.Interfaces;
using optimizerDuck.Models;
using optimizerDuck.UI.Logger;

namespace optimizerDuck.Core.Optimizers;

public class UserExperience : IOptimizationGroup
{
    public string Name { get; } = "User Experience";
    public int Order { get; } = (int)OptimizationGroupOrder.UserExperience;
    public static ILogger Log { get; } = Logger.CreateLogger<UserExperience>();

    public class TaskbarTweak : IOptimizationTweak
    {
        public string Name { get; } = "Taskbar Optimization";
        public string Description { get; } = "Simplifies and cleans up the Windows taskbar for better performance";
        public bool EnabledByDefault { get; } = true;
        public OptimizationImpact Impact { get; } = OptimizationImpact.Minimal;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin(Log);
            RegistryService.Write(
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarMn", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings", "TaskbarEndTask", 1),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", "CortanaConsent", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", "CortanaConsent2", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowTaskViewButton", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced\People", "PeopleBand", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "HideSCAMeetNow", 1),
                new RegistryItem(@"HKCU\SOFTWARE\Policies\Microsoft\Windows\Windows Feeds", "EnableFeeds", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Feeds", "ShellFeedsTaskbarViewMode", 2),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewShadow", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 0)
            );
            Log.LogInformation("Taskbar cleaned up and simplified.");
            return Task.CompletedTask;
        }
    }

    public class DarkModeTweak : IOptimizationTweak
    {
        public string Name { get; } = "Dark Mode";
        public string Description { get; } = "Forces Windows to use dark mode for apps and system";
        public bool EnabledByDefault { get; } = true;
        public OptimizationImpact Impact { get; } = OptimizationImpact.Minimal;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin(Log);
            RegistryService.Write(
                new RegistryItem(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0),
                new RegistryItem(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0)
            );
            Log.LogInformation("Switched to dark mode.");
            return Task.CompletedTask;
        }
    }

    public class ExplorerTweak : IOptimizationTweak
    {
        public string Name { get; } = "Explorer Optimization";
        public string Description { get; } = "Optimizes Windows Explorer visuals and usability";
        public bool EnabledByDefault { get; } = true;
        public OptimizationImpact Impact { get; } = OptimizationImpact.Minimal;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin(Log);
            RegistryService.Write(
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSyncProviderNotifications", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0),
                new RegistryItem(@"HKCU\Control Panel\Desktop\WindowMetrics", "MinAnimate", "0"),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0),
                new RegistryItem(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Hidden", 1),
                new RegistryItem(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0),
                new RegistryItem(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSecondsInSystemClock", 1),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowInfoTip", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", ""), // old Win10 context menu
                new RegistryItem(@"HKCU\Control Panel\Desktop", "MenuShowDelay", "0")
            );
            Log.LogInformation("Explorer visuals optimized.");
            return Task.CompletedTask;
        }
    }


    public class VisualPerformanceTweak : IOptimizationTweak
    {
        public string Name { get; } = "Visual Performance";
        public string Description { get; } = "Sets Windows visual effects for best performance";
        public bool EnabledByDefault { get; } = true;
        public OptimizationImpact Impact { get; } = OptimizationImpact.Minimal;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin(Log);
            RegistryService.Write(
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 3),
                new RegistryItem(@"HKCU\Control Panel\Desktop", "DragFullWindows", "0"),
                new RegistryItem(@"HKCU\Control Panel\Desktop", "UserPreferencesMask", new byte[] { 144, 18, 3, 128, 16, 0, 0, 0 }, RegistryValueKind.Binary)
            );
            Log.LogInformation("Visual performance set to best.");
            return Task.CompletedTask;
        }
    }



    public class DisableNotifications : IOptimizationTweak
    {
        public string Name { get; } = "Disable Notifications";

        public string Description { get; } =
            "Disables Windows notifications and action center to minimize distractions during gaming";

        public bool EnabledByDefault { get; } = true;
        public OptimizationImpact Impact { get; } = OptimizationImpact.Minimal;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin(Log);

            RegistryService.Write(
                new RegistryItem(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\PushNotifications",
                    "ToastEnabled", 0),
                new RegistryItem(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\PushNotifications",
                    "LockScreenToastEnabled", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "EnableBalloonTips", 0)
            );
            Log.LogInformation("Notifications disabled.");
            return Task.CompletedTask;
        }
    }

    public class OptimizeMouse : IOptimizationTweak
    {
        public string Name { get; } = "Optimize Mouse";

        public string Description { get; } =
            "Reduces mouse input lag and improves accuracy by disabling mouse acceleration and adjusting sensitivity";

        public bool EnabledByDefault { get; } = true;
        public OptimizationImpact Impact { get; } = OptimizationImpact.Minimal;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin(Log);

            RegistryService.Write(
                new RegistryItem(@"HKCU\Control Panel\Mouse", "MouseSpeed", "0"),
                new RegistryItem(@"HKCU\Control Panel\Mouse", "MouseThreshold1", "0"),
                new RegistryItem(@"HKCU\Control Panel\Mouse", "MouseThreshold2", "0"),
                new RegistryItem(@"HKCU\Control Panel\Mouse", "MouseSensitivity", "10")
            );
            Log.LogInformation("Mouse acceleration disabled.");
            return Task.CompletedTask;
        }
    }

    public class OptimizeKeyboard : IOptimizationTweak
    {
        public string Name { get; } = "Optimize Keyboard";

        public string Description { get; } =
            "Improves keyboard responsiveness and disables unwanted accessibility features";

        public bool EnabledByDefault { get; } = true;
        public OptimizationImpact Impact { get; } = OptimizationImpact.Minimal;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin(Log);

            RegistryService.Write(
                new RegistryItem(@"HKEY_CURRENT_USER\Control Panel\Keyboard", "KeyboardDelay", "0"),
                new RegistryItem(@"HKEY_CURRENT_USER\Control Panel\Keyboard", "KeyboardSpeed", "31"),
                new RegistryItem(@"HKEY_CURRENT_USER\Control Panel\Accessibility\StickyKeys", "Flags", "506"),
                new RegistryItem(@"HKEY_CURRENT_USER\Control Panel\Accessibility\Keyboard Response", "Flags", "122"),
                new RegistryItem(@"HKEY_CURRENT_USER\Control Panel\Accessibility\ToggleKeys", "Flags", "58")
            );
            Log.LogInformation("Keyboard accessibility features disabled.");
            return Task.CompletedTask;
        }
    }

    public class InstallZwTimerResolution : IOptimizationTweak
    {
        public string Name => "Install ZwTimerResolution";

        public string Description =>
            "Installs and configures ZwTimerResolution to reduce input lag and improve system responsiveness";

        public bool EnabledByDefault => true;
        public OptimizationImpact Impact { get; } = OptimizationImpact.Moderate;

        public async Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin(Log);

            var (success, zwtPath) = await StreamHelper.TryDownloadAsync(Defaults.ZwtDownloadUrl, "zwtimer.exe")
                .ConfigureAwait(false);
            if (success && !string.IsNullOrEmpty(zwtPath))
            {
                Log.LogInformation("ZwTimerResolution downloaded successfully!");
                /*
                 https://github.com/LuSlower/ZwTimerResolution/tree/main
                 If you have Windows Server 2022/Windows 11 you must have in your registry:
                 [HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\kernel] "GlobalTimerResolutionRequests"=dword:00000001
                */
                if (SystemHelper.IsWindows11OrGreater()) // i dont check for server versions..................
                    RegistryService.Write(new RegistryItem(
                        @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\kernel",
                        "GlobalTimerResolutionRequests", 1));

                RegistryService.Write(
                    new RegistryItem(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run", "ZwTimer",
                        zwtPath),
                    new RegistryItem(@"HKEY_CURRENT_USER\Software\ZwTimer", "Start", "1"),
                    new RegistryItem(@"HKEY_CURRENT_USER\Software\ZwTimer", "CustomTimer", "5052")
                );

                Log.LogInformation("Set ZwTimerResolution Start with System and value to 5052hns.");
            }
            else
            {
                Log.LogError("Failed to download ZwTimerResolution! Skipping...");
            }
        }
    }
}