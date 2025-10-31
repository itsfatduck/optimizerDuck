using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using optimizerDuck.Core.Helpers;
using optimizerDuck.Core.Services;
using optimizerDuck.Interfaces;
using optimizerDuck.Models;
using optimizerDuck.UI;
using optimizerDuck.UI.Logger;

namespace optimizerDuck.Core.Optimizers;

public class PerformanceAndPower : IOptimizationGroup
{
    public string Name => "Performance & Power";
    public int Priority => (int)OptimizationGroupPriority.PerformanceAndPower;
    public static ILogger Log { get; } = Logger.CreateLogger<PerformanceAndPower>();

    public class PerformanceTweaks : IOptimizationTweak
    {
        public string Name { get; } = "Performance Tweaks";

        public string Description { get; } =
            "Applies performance tweaks to optimize Windows for speed, responsiveness, and smoother gaming";

        public bool EnabledByDefault { get; } = true;

        public Task Apply(SystemSnapshot s)
        {
            RegistryService.Write(
                new RegistryItem(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications",
                    "GlobalUserDisabled", 1),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle",
                    0)
            );
            Log.LogInformation("Disabled background applications");

            /*
             ref: https://forums.blurbusters.com/viewtopic.php?t=8535
            42 Dec, 2A Hex = Short, Fixed , High foreground boost.
            41 Dec, 29 Hex = Short, Fixed , Medium foreground boost.
            40 Dec, 28 Hex = Short, Fixed , No foreground boost.

            38 Dec, 26 Hex = Short, Variable , High foreground boost.
            37 Dec, 25 Hex = Short, Variable , Medium foreground boost.
            36 Dec, 24 Hex = Short, Variable , No foreground boost.

            26 Dec, 1A Hex = Long, Fixed, High foreground boost.
            25 Dec, 19 Hex = Long, Fixed, Medium foreground boost.
            24 Dec, 18 Hex = Long, Fixed, No foreground boost.

            22 Dec, 16 Hex = Long, Variable, High foreground boost.
            21 Dec, 15 Hex = Long, Variable, Medium foreground boost.
            20 Dec, 14 Hex = Long, Variable, No foreground boost.
             */

            const int win32Priority = 38;

            RegistryService.Write(
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation",
                    win32Priority),
                new RegistryItem(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                    "Priority", 6),
                new RegistryItem(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                    "Scheduling Category", "High"),
                new RegistryItem(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                    "SFIO Priority", "High"),
                new RegistryItem(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                    "GPU Priority", 8),
                new RegistryItem(@"HKCU\Control Panel\Desktop", "AutoEndTasks", 1),
                new RegistryItem(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                    "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord),
                new RegistryItem(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                    "SystemResponsiveness", 0),
                new RegistryItem(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode",
                    2) // idk
            );
            Log.LogInformation("Applied performance tweaks.");

            if (s.Ram.TotalKB > 0)
            {
                RegistryService.Write(new RegistryItem(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control",
                    "SvcHostSplitThresholdInKB", s.Ram.TotalKB, RegistryValueKind.DWord));
                Log.LogInformation("Set SvcHostSplitThresholdInKB to {SvcHostSplitThresholdInKB}.", s.Ram.TotalKB);
            }

            if (int.TryParse(s.Os.Version, out var version) && version >= 11)
            {
                RegistryService.Write(
                    new RegistryItem(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AllowAutoGameMode", 1),
                    new RegistryItem(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AutoGameModeEnabled", 1)
                );
                Log.LogInformation("Enabled Game Mode.");
            }

            return Task.CompletedTask;
        }
    }

    public class InstallOptimizerDuckPowerPlan : IOptimizationTweak
    {
        public string Name { get; } = "Install optimizerDuck Power Plan";

        public string Description { get; } =
            "Installs a custom high-performance power plan optimized for gaming and maximum CPU performance";

        public bool EnabledByDefault { get; } = true;

        public async Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin();
            var (success, powerPlanPath) =
                await StreamHelper.TryDownloadAsync(Defaults.PowerPlanUrl, "optimizerDuck.pow");
            if (success && !string.IsNullOrEmpty(powerPlanPath))
            {
                // set other power plan to delete old one (if exists)
                ShellService.CMD("powercfg /duplicatescheme 381b4222-f694-41f0-9685-ff5bb260df2e"); // Balanced
                ShellService.CMD("powercfg /setactive 381b4222-f694-41f0-9685-ff5bb260df2e"); // Balanced
                ShellService.CMD($"powercfg /delete {Defaults.PowerPlanGUID}");
                Log.LogInformation("Deleted old power plan.");

                ShellService.CMD($"powercfg /import \"{powerPlanPath}\" {Defaults.PowerPlanGUID}");
                ShellService.CMD($"powercfg /setactive {Defaults.PowerPlanGUID}");
                Log.LogInformation($"Installed [{Theme.Primary}]optimizerDuck[/] power plan successfully!");
            }
            else
            {
                Log.LogError("Failed to download optimizerDuck power plan! Skipping...");
            }
        }
    }

    public class DisablePowerSavingTweak : IOptimizationTweak
    {
        public string Name { get; } = "Disable Power Saving";

        public string Description { get; } =
            "Disables power throttling and enables AlwaysOn multimedia mode for maximum responsiveness.";

        public bool EnabledByDefault { get; } = true;

        public Task Apply(SystemSnapshot s)
        {
            RegistryService.Write(
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\USB\AutomaticSurpriseRemoval",
                    "AttemptRecoveryFromUsbPowerDrain", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                    "NoLazyMode", 1),
                new RegistryItem(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                    "AlwaysOn", 1),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff",
                    1)
            );

            Log.LogInformation("Disabled power saving features.");
            return Task.CompletedTask;
        }
    }
    
    public class DisableHibernateTweak : IOptimizationTweak
    {
        public string Name { get; } = "Disable Hibernate";

        public string Description { get; } =
            "Disables hibernate, fast startup, and removes hibernate option from power menu.";

        public bool EnabledByDefault { get; } = true;

        public Task Apply(SystemSnapshot s)
        {
            RegistryService.Write(
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling",
                    "HibernateEnabledDefault", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings",
                    "ShowHibernateOption", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0)
            );

            Log.LogInformation("Disabled hibernate & fast startup.");
            return Task.CompletedTask;
        }
    }

    public class DisableUSBPowerSaving : IOptimizationTweak
    {
        public string Name { get; } = "Disable USB Power Saving";

        public string Description { get; } =
            "Disables USB selective suspend and other power management features that can cause input lag and performance drops";

        public bool EnabledByDefault { get; } = true;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin();
            //https://discord.com/channels/1298592513816530994/1359513721738629140/1359524213047824517
            //from FrameSync Labs
            ShellService.PowerShell("""
                                    $devicesUSB = Get-PnpDevice | where {$_.InstanceId -like "*USB\ROOT*"}  | 
                                    ForEach-Object -Process {
                                    Get-CimInstance -ClassName MSPower_DeviceEnable -Namespace root\wmi 
                                    }
                                    foreach ( $device in $devicesUSB )
                                    {
                                        Set-CimInstance -Namespace root\wmi -Query "SELECT * FROM MSPower_DeviceEnable WHERE InstanceName LIKE '%$($device.PNPDeviceID)%'" -Property @{Enable=$False} -PassThru
                                    }
                                    """);
            Log.LogInformation("Disabled USB power saving.");
            return Task.CompletedTask;
        }
    }
}