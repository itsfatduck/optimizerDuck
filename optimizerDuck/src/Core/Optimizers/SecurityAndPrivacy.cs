using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Services;
using optimizerDuck.Interfaces;
using optimizerDuck.Models;
using optimizerDuck.UI.Logger;

namespace optimizerDuck.Core.Optimizers;

public class SecurityAndPrivacy : IOptimizationCategory
{
    public string Name { get; } = "Security & Privacy";
    public OptimizationCategoryOrder Order { get; } = OptimizationCategoryOrder.SecurityAndPrivacy;
    public static ILogger Log { get; } = Logger.CreateLogger<SecurityAndPrivacy>();

    public class DisableUAC : IOptimization
    {
        public string Name { get; } = "Disable User Account Control (UAC)";

        public string Description { get; } =
            "Disables UAC prompts to reduce interruptions (reduces security - use with caution)";

        public bool EnabledByDefault { get; } = false;
        public OptimizationImpact Impact { get; } = OptimizationImpact.Aggressive;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin(Log);

            RegistryService.Write(new RegistryItem(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                "EnableLUA", 0));
            Log.LogInformation("Disabled UAC");
            return Task.CompletedTask;
        }
    }

    public class DisableTelemetry : IOptimization
    {
        public string Name { get; } = "Disable Telemetry Services & Tasks";

        public string Description { get; } =
            "Disables telemetry services, scheduled tasks, and Windows data collection";

        public bool EnabledByDefault { get; } = true;
        public OptimizationImpact Impact { get; } = OptimizationImpact.Significant;

        /// <summary>
        ///     thank you https://github.com/ChrisTitusTech/winutil
        /// </summary>
        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin(Log);
            // @formatter:off
            RegistryService.Write(
                new RegistryItem(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications", 1),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowCommercialDataPipeline", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowDeviceNameInTelemetry", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "MicrosoftEdgeDataOptIn", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0),
                new RegistryItem(@"HKCU\Software\Policies\Microsoft\Windows\EdgeUI", "DisableMFUTracking", 1),
                new RegistryItem(@"HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\AppCompat", "DisableInventory", 1),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\AppCompat", "AITEnable", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableTailoredExperiencesWithDiagnosticData", 1),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableSoftLanding", 1),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableThirdPartySuggestions", 1),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Feeds", "ShellFeedsTaskbarViewMode", 2),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "HideSCAMeetNow", 1),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation", 1),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableSensors", 1),
                new RegistryItem(@"HKLM\SYSTEM\Maps", "AutoUpdateEnabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Sensor\Permissions\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}", "SensorPermissionState", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivitiesOnUserConsent", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Policies\Microsoft\Assistance\Client\1.0", "NoExplicitFeedback", 1),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Assistance\Client\1.0", "NoActiveHelp", 1),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\HandwritingErrorReports", "PreventHandwritingErrorReports", 1),
                new RegistryItem(@"HKCU\Control Panel\International\User Profile", "HttpAcceptLanguageOptOut", 1),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "Value", "Deny"),
                new RegistryItem(@"HKLM\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "Value", "Deny"),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Geolocation", "Status", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Services\lfsvc\Service\Configuration", "Status", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy", "01", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy", "02", 0),
                new RegistryItem(@"HKLM\Software\Microsoft\PolicyManager\default\WiFi\AllowWiFiHotSpotReporting", "Value", 0),
                new RegistryItem(@"HKLM\Software\Microsoft\PolicyManager\default\WiFi\AllowAutoConnectToWiFiSenseHotspots", "Value", 0)
            );
            RegistryService.DeleteValue([
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Siuf\Rules", "PeriodInNanoSeconds")
            ]);

            ServiceProcessService.ChangeServiceStartupType(
                new ServiceItem("DiagTrack", ServiceStartupType.Disabled),  // Connected User Experiences and Telemetry (The main telemetry service)
                new ServiceItem("dmwappushservice", ServiceStartupType.Disabled),  //  WAP Push Message Routing Service (Used for telemetry)
                new ServiceItem("DcpSvc", ServiceStartupType.Disabled),  //  Data Collection and Publishing Service
                new ServiceItem("diagnosticshub.standardcollector.service", ServiceStartupType.Disabled),  //  Diagnostic Hub Standard Collector Service
                new ServiceItem("DusmSvc", ServiceStartupType.Disabled),  //  Data Usage (Monitors app data usage)
                new ServiceItem("WerSvc", ServiceStartupType.Disabled),  //  Windows Error Reporting Service (Sends error data to Microsoft)
                new ServiceItem("PcaSvc", ServiceStartupType.Disabled) //  Program Compatibility Assistant Service (Monitors apps and sends data)
            );

            var tasksToDelete = new HashSet<string>([
                @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
                @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
                @"\Microsoft\Windows\Application Experience\MareBackup",
                @"\Microsoft\Windows\Application Experience\StartupAppTask",
                @"\Microsoft\Windows\Application Experience\PcaPatchDbTask",
                @"\Microsoft\Windows\Autochk\Proxy",
                @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
                @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
                @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector",
                @"\Microsoft\Windows\Feedback\Siuf\DmClient",
                @"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload",
                @"\Microsoft\Windows\Windows Error Reporting\QueueReporting",
                @"\Microsoft\Windows\Maps\MapsUpdateTask",
                @"\Microsoft\Windows\Diagnosis\Scheduled",
                @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticResolver"
                ]);

            foreach (var task in tasksToDelete)
                if (ShellService.CMD($"schtasks /Change /TN \"{task}\" /Disable").ExitCode != 0)
                    Log.LogError("Failed to disable task {Task}.", task);

            Log.LogInformation("Disabled telemetry.");
            // @formatter:on
            return Task.CompletedTask;
        }
    }

    public class DisableAutologger : IOptimization
    {
        public string Name { get; } = "Disable WMI AutoLogger";

        public string Description { get; } =
            "Disables WMI AutoLogger services used for diagnostic event tracing";

        public bool EnabledByDefault { get; } = true;
        public OptimizationImpact Impact { get; } = OptimizationImpact.Moderate;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin(Log);

            RegistryService.Write(
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\AppModel", "Start", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\Cellcore", "Start", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\CloudExperienceHostOobe", "Start", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\DataMarket", "Start", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\DefenderApiLogger", "Start", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\DefenderAuditLogger", "Start", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\DiagLog", "Start", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\Diagtrack-Listener", "Start", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\LwtNetLog", "Start", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\SQMLogger", "Start", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\WdiContextLog", "Start", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\WiFiSession", "Start", 0)
                );
            Log.LogInformation("Disabled Autologger services.");
            return Task.CompletedTask;
        }
    }
    public class DisableContentDeliveryManager : IOptimization
    {
        public string Name { get; } = "Disable Content Delivery Manager";

        public string Description { get; } =
            "Disables suggested apps, consumer features, and content recommendations";

        public bool EnabledByDefault { get; } = true;
        public OptimizationImpact Impact { get; } = OptimizationImpact.Moderate;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin(Log);

            RegistryService.Write(
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "ContentDeliveryAllowed", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "OemPreInstalledAppsEnabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "PreInstalledAppsEnabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "PreInstalledAppsEverEnabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338387Enabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353698Enabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0)
                );
            Log.LogInformation("Disabled Content Delivery Manager.");
            return Task.CompletedTask;
        }
    }

    public class DisableCortana : IOptimization
    {
        public string Name { get; } = "Disable Cortana & Search AI";

        public string Description { get; } =
            "Disables Cortana, cloud search, web search integration, and related AI-powered search features";

        public bool EnabledByDefault { get; } = true;
        public OptimizationImpact Impact { get; } = OptimizationImpact.Moderate;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin(Log);

            RegistryService.Write(
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCloudSearch", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortanaAboveLock", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowSearchToUseLocation", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "ConnectedSearchUseWeb", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search",
                    "ConnectedSearchUseWebOverMeteredConnections", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "DisableWebSearch", 1)
            );

            Log.LogInformation("Cortana and AI-powered search features disabled.");
            return Task.CompletedTask;
        }
    }

    public class DisableCopilot : IOptimization
    {
        public string Name { get; } = "Disable Windows Copilot";

        public string Description { get; } =
            "Disables AI Copilot integration from Explorer and system taskbar";

        public bool EnabledByDefault { get; } = true;
        public OptimizationImpact Impact { get; } = OptimizationImpact.Moderate;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin(Log);

            RegistryService.Write(
                 new RegistryItem(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1),
                 new RegistryItem(@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1),
                 new RegistryItem(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCopilotButton", 0),
                 new RegistryItem(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Shell\Copilot", "IsCopilotAvailable", 0),
                 new RegistryItem(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Shell\Copilot", "CopilotDisabledReason", "IsEnabledForGeographicRegionFailed"),
                 new RegistryItem(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsCopilot", "AllowCopilotRuntime", 0),
                 new RegistryItem(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked", "{CB3B0003-8088-4EDE-8769-8B354AB2FF8C}", ""),
                 new RegistryItem(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Shell\Copilot\BingChat", "IsUserEligible", 0)
            );

            Log.LogInformation("Windows Copilot disabled.");
            return Task.CompletedTask;
        }
    }
}