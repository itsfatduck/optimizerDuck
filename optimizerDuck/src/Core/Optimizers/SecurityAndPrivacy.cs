using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Services;
using optimizerDuck.Interfaces;
using optimizerDuck.Models;
using optimizerDuck.UI.Logger;

namespace optimizerDuck.Core.Optimizers;

public class SecurityAndPrivacy : IOptimizationGroup
{
    public string Name => "Security & Privacy";
    public int Priority => (int)OptimizationGroupPriority.SecurityAndPrivacy;
    public static ILogger Log { get; } = Logger.CreateLogger<SecurityAndPrivacy>();

    public class DisableUAC : IOptimizationTweak
    {
        public string Name { get; } = "Disable UAC";

        public string Description { get; } =
            "Disables User Account Control to reduce interruptions and improve system responsiveness (dangerous)";

        public bool EnabledByDefault { get; } = false;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin();
            RegistryService.Write(new RegistryItem(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                "EnableLUA", 0));
            Log.LogInformation("Disabled UAC");
            return Task.CompletedTask;
        }
    }

    public class DisableTelemetry : IOptimizationTweak
    {
        public string Name { get; } = "Disable Telemetry";

        public string Description { get; } =
            "Disables Windows telemetry and data collection to improve privacy and reduce background network usage";

        public bool EnabledByDefault { get; } = true;

        /// <summary>
        ///     thank you https://github.com/ChrisTitusTech/winutil
        /// </summary>
        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin();
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
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "ContentDeliveryAllowed", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "OemPreInstalledAppsEnabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "PreInstalledAppsEnabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "PreInstalledAppsEverEnabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338387Enabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353698Enabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0),
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
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\WiFiSession", "Start", 0),
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

            Log.LogInformation("Disabled almost all telemetry.");
            // @formatter:on
            return Task.CompletedTask;
        }
    }

    public class DisableCortanaTweak : IOptimizationTweak
    {
        public string Name { get; } = "Disable Cortana & Search AI";

        public string Description { get; } =
            "Disables Cortana, cloud search, web search integration, and related AI-powered search features.";

        public bool EnabledByDefault { get; } = true;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin();

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
    
    public class DisableCopilotTweak : IOptimizationTweak
    {
        public string Name { get; } = "Disable Windows Copilot";

        public string Description { get; } =
            "Disables Windows Copilot integration in Explorer and system UI.";

        public bool EnabledByDefault { get; } = true;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin();

            RegistryService.Write(
                new RegistryItem(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "TurnOffWindowsCopilot", 1)
            );

            Log.LogInformation("Windows Copilot disabled.");
            return Task.CompletedTask;
        }
    }
}