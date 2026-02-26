using System.Collections.ObjectModel;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.Optimization;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Services.Managers;
using optimizerDuck.Services.OptimizationServices;
using optimizerDuck.UI.Views.Pages.Optimizations;

namespace optimizerDuck.Core.Optimizers;

[OptimizationCategory(typeof(SecurityAndPrivacyOptimizerPage))]
public class SecurityAndPrivacy : IOptimizationCategory
{
    public string Name { get; init; } = Loc.Instance[$"Optimizer.{nameof(SecurityAndPrivacy)}"];
    public OptimizationCategoryOrder Order { get; init; } = OptimizationCategoryOrder.SecurityAndPrivacy;
    public ObservableCollection<IOptimization> Optimizations { get; init; } = [];

    [Optimization(Id = "09F7CE38-93B2-4E1A-AB09-130268165B42", Risk = OptimizationRisk.Risky,
        Tags = OptimizationTags.System | OptimizationTags.Security)]
    public class DisableUAC : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress,
            OptimizationContext context)
        {
            RegistryService.Write(new RegistryItem(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                "EnableLUA", 0));
            context.Logger.LogInformation("Disabled UAC prompts");
            return Task.FromResult(ApplyResult.True());
        }
    }

    [Optimization(Id = "74DC8DAC-1F90-4BBD-ACF7-7E626749D71C",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Security | OptimizationTags.Privacy)]
    public class DisableTelemetry : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress,
            OptimizationContext context)
        {
            progress?.Report(new ProcessingProgress
            {
                Message = Loc.Instance[$"{ProgressPrefix}.EditRegistry"],
                IsIndeterminate = false,
                Value = 0,
                Total = 3
            });
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
            context.Logger.LogInformation("Reduced telemetry and feedback");
            progress?.Report(new ProcessingProgress
            {
                Message = Loc.Instance[$"{ProgressPrefix}.DisableServices"],
                IsIndeterminate = false,
                Value = 1,
                Total = 3
            });
            ServiceProcessService.ChangeServiceStartupType(
                new ServiceItem("DiagTrack", ServiceStartupType.Disabled),  // Connected User Experiences and Telemetry (The main telemetry service)
                new ServiceItem("dmwappushservice", ServiceStartupType.Disabled),  //  WAP Push Message Routing Service (Used for telemetry)
                new ServiceItem("DcpSvc", ServiceStartupType.Disabled),  //  Data Collection and Publishing Service
                new ServiceItem("diagnosticshub.standardcollector.service", ServiceStartupType.Disabled),  //  Diagnostic Hub Standard Collector Service
                new ServiceItem("DusmSvc", ServiceStartupType.Disabled),  //  Data Usage (Monitors app data usage)
                new ServiceItem("WerSvc", ServiceStartupType.Disabled),  //  Windows Error Reporting Service (Sends error data to Microsoft)
                new ServiceItem("PcaSvc", ServiceStartupType.Disabled) //  Program Compatibility Assistant Service (Monitors apps and sends data)
            );

            progress?.Report(new ProcessingProgress
            {
                Message = Loc.Instance[$"{ProgressPrefix}.DisableTasks"],
                IsIndeterminate = false,
                Value = 2,
                Total = 3
            });
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
            // @formatter:off

            foreach (var task in tasksToDelete)
            {
                var query = ShellService.CMD(
                    $"schtasks /Query /TN \"{task}\" /XML"
                );

                if (query.ExitCode != 0 || string.IsNullOrWhiteSpace(query.Stdout))
                {
                    context.Logger.LogDebug("Task {Task} not found, skipping", task);
                    continue;
                }

                var wasEnabled = GetTaskEnabledFromXml(query.Stdout);

                if (wasEnabled == null)
                {
                    context.Logger.LogWarning(
                        "Cannot determine Enabled state from XML for task {Task}, skipping",
                        task
                    );
                    continue;
                }

                var result = ShellService.CMD(
                    $"schtasks /Change /TN \"{task}\" /Disable",
                    () => wasEnabled.Value
                        ? $"schtasks /Change /TN \"{task}\" /Enable"
                        : $"schtasks /Change /TN \"{task}\" /Disable"
                );

                if (result.ExitCode != 0)
                    context.Logger.LogWarning(
                        "Failed to disable task {Task}",
                        task
                    );
            }

            return Task.FromResult(ApplyResult.True());
        }

        private static bool? GetTaskEnabledFromXml(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);

                // Task Scheduler XML has a namespace
                var ns = doc.Root!.Name.Namespace;

                var enabledElement = doc
                    .Element(ns + "Task")?
                    .Element(ns + "Settings")?
                    .Element(ns + "Enabled");

                // if element is not found, it means the task is enabled
                return enabledElement == null || bool.Parse(enabledElement.Value);
            }
            catch
            {
                return null;
            }
        }
    }

    [Optimization(Id = "4107430C-0074-4380-90E7-3662572E4720", Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Privacy)]
    public class DisableAutoLogger : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\AppModel", "Start", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\Cellcore", "Start", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\CloudExperienceHostOobe",
                    "Start", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\DataMarket", "Start", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\DefenderApiLogger", "Start", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\DefenderAuditLogger", "Start",
                    0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\DiagLog", "Start", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\Diagtrack-Listener", "Start",
                    0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\LwtNetLog", "Start", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\SQMLogger", "Start", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\WdiContextLog", "Start", 0),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\WiFiSession", "Start", 0)
            );
            context.Logger.LogInformation("Disabled WMI AutoLogger sessions");
            return Task.FromResult(ApplyResult.True());
        }}

    [Optimization(Id = "6856782A-B530-4623-BD89-942D73FB82FD", Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Privacy | OptimizationTags.System)]
    public class DisableCortana : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                 new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0),
                 new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCloudSearch", 0),
                 new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortanaAboveLock",
                     0),
                 new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowSearchToUseLocation",
                     0),
                 new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "ConnectedSearchUseWeb",
                     0),
                  new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "DisableWebSearch", 1),
                  new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search",
                     "CortanaConsent", 0),
                 new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search",
                     "CortanaConsent2", 0)
             );
            context.Logger.LogInformation("Disabled Cortana and web search");
            return Task.FromResult(ApplyResult.True());
        }}

    [Optimization(Id = "64C6BEC3-B58C-4E57-830A-1DE1F4650542", Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Privacy | OptimizationTags.System)]
    public class DisableCopilot : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new RegistryItem(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot",
                    "TurnOffWindowsCopilot", 1),
                new RegistryItem(@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\WindowsCopilot",
                    "TurnOffWindowsCopilot", 1),
                new RegistryItem(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "ShowCopilotButton", 0),
                new RegistryItem(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Shell\Copilot", "IsCopilotAvailable",
                    0),
                new RegistryItem(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Shell\Copilot",
                    "CopilotDisabledReason", "IsEnabledForGeographicRegionFailed"),
                new RegistryItem(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsCopilot",
                    "AllowCopilotRuntime", 0),
                new RegistryItem(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked",
                    "{CB3B0003-8088-4EDE-8769-8B354AB2FF8C}", ""),
                new RegistryItem(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Shell\Copilot\BingChat",
                    "IsUserEligible", 0)
            );

            context.Logger.LogInformation("Disabled Windows Copilot");
            return Task.FromResult(ApplyResult.True());
        }}

    [Optimization(Id = "00C997FE-1CB7-41BD-B473-65A81333AEE9", Risk = OptimizationRisk.Safe,
    Tags = OptimizationTags.System | OptimizationTags.Latency | OptimizationTags.Privacy)]
    public class DisableContentDeliveryManager : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                    "ContentDeliveryAllowed", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                    "SubscribedContent-338387Enabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                    "SubscribedContent-338388Enabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                    "SubscribedContent-338389Enabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                    "SubscribedContent-353698Enabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                    "SystemPaneSuggestionsEnabled", 0)
            );
            context.Logger.LogInformation("Disabled content delivery manager");
            return Task.FromResult(ApplyResult.True());
        }}
}