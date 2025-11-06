using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Helpers;
using optimizerDuck.Core.Services;
using optimizerDuck.Interfaces;
using optimizerDuck.Models;
using optimizerDuck.UI;
using optimizerDuck.UI.Logger;

namespace optimizerDuck.Core.Optimizers;

public class Power : IOptimizationGroup
{
    public string Name { get; } = "Power";
    public int Order { get; } = (int)OptimizationGroupOrder.Power;
    public static ILogger Log { get; } = Logger.CreateLogger<Power>();


    public class DisableHibernateTweak : IOptimizationTweak
    {
        public string Name { get; } = "Disable Hibernate";

        public string Description { get; } =
            "Disables hibernate, fast startup, and removes hibernate option from power menu";

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
            using var tracker = ServiceTracker.Begin(Log);
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


    public class InstallOptimizerDuckPowerPlan : IOptimizationTweak
    {
        public string Name { get; } = "Install optimizerDuck Power Plan";

        public string Description { get; } =
            "Installs a custom high-performance power plan optimized for gaming and maximum CPU performance";

        public bool EnabledByDefault { get; } = true;

        public async Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin(Log);
            var (success, powerPlanPath) =
                await StreamHelper.TryDownloadAsync(Defaults.PowerPlanUrl, "optimizerDuck.pow");
            if (success && !string.IsNullOrEmpty(powerPlanPath))
            {
                // set other power plan to delete old one (to avoid import errors)
                ShellService.CMD("powercfg /duplicatescheme 381b4222-f694-41f0-9685-ff5bb260df2e"); // restore Balanced
                ShellService.CMD("powercfg /setactive 381b4222-f694-41f0-9685-ff5bb260df2e"); // set Balanced
                ShellService.CMD($"powercfg /delete {Defaults.PowerPlanGUID}");
                Log.LogInformation("Deleted old power plan.");

                ShellService.CMD($"powercfg /import \"{powerPlanPath}\" {Defaults.PowerPlanGUID}");
                ShellService.CMD($"powercfg /setactive {Defaults.PowerPlanGUID}");
                Log.LogInformation($"Installed [{Theme.Primary}]optimizerDuck[/] power plan successfully!");
            }
            else
            {
                Log.LogError($"Failed to download [{Theme.Primary}]optimizerDuck[/] power plan! Skipping...");
            }
        }
    }

    public class DisablePowerSavingTweak : IOptimizationTweak
    {
        public string Name { get; } = "Disable Power Saving";

        public string Description { get; } =
            "Disables power throttling and enables AlwaysOn multimedia mode for maximum responsiveness";

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
}