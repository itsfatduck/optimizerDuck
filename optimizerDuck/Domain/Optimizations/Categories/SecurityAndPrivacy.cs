using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Conditions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.UI.Pages.Optimize.Categories;

namespace optimizerDuck.Domain.Optimizations.Categories;

[OptimizationCategory(typeof(SecurityAndPrivacyOptimizerPage))]
public class SecurityAndPrivacy()
    : OptimizationCategoryBase(OptimizationCategoryOrder.SecurityAndPrivacy)
{
    [Optimization(
        Id = "74DC8DAC-1F90-4BBD-ACF7-7E626749D71C",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Security | OptimizationTags.Privacy | OptimizationTags.System
    )]
    public class DisableTelemetry : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(
                @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
                "AllowTelemetry",
                0
            ),
            new(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0),
            new(
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                "DoNotShowFeedbackNotifications",
                1
            ),
            new(
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                "AllowCommercialDataPipeline",
                0
            ),
            new(
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                "AllowDeviceNameInTelemetry",
                0
            ),
            new(
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                "MicrosoftEdgeDataOptIn",
                0
            ),
            new(@"HKCU\SOFTWARE\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0),
            new(@"HKCU\Software\Policies\Microsoft\Windows\EdgeUI", "DisableMFUTracking", 1),
            new(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\AppCompat", "DisableInventory", 1),
            new(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\AppCompat", "AITEnable", 0),
            new(@"HKCU\SOFTWARE\Policies\Microsoft\Assistance\Client\1.0", "NoExplicitFeedback", 1),
            new(@"HKLM\SOFTWARE\Policies\Microsoft\Assistance\Client\1.0", "NoActiveHelp", 1),
            new(
                @"HKCU\SOFTWARE\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy",
                "HasAccepted",
                0
            ),
            new(
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                "Start_TrackProgs",
                0
            ),
        ];

        protected override RegistryItem[] DeleteRegistryValues { get; } =
        [new(@"HKCU\SOFTWARE\Microsoft\Siuf\Rules", "PeriodInNanoSeconds")];

        protected override ServiceItem[] Services { get; } =
        [
            ServiceItem.Disable("DiagTrack"),
            ServiceItem.Disable("dmwappushservice"),
            ServiceItem.Disable("DcpSvc"),
            ServiceItem.Disable("diagnosticshub.standardcollector.service"),
        ];

        protected override string[] ScheduledTasks { get; } =
        [
            @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
            @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
            @"\Microsoft\Windows\Application Experience\MareBackup",
            @"\Microsoft\Windows\Application Experience\StartupAppTask",
            @"\Microsoft\Windows\Application Experience\PcaPatchDbTask",
            @"\Microsoft\Windows\Autochk\Proxy",
            @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
            @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
            @"\Microsoft\Windows\Feedback\Siuf\DmClient",
            @"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload",
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Privacy),
                    Improves(ImpactArea.Performance),
                    Improves(ImpactArea.Network),
                ],
                Sources =
                [
                    Source(
                        "Configure Windows diagnostic data - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/privacy/configure-windows-diagnostic-data-in-your-organization"
                    ),
                ],
                RequiresRestart = RestartRequirement.Reboot,
            };
    }

    [Optimization(
        Id = "097B1C61-F372-4B9E-A1C9-D5E6F7A8B9C0",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.System | OptimizationTags.Privacy
    )]
    public class DisableErrorReporting : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(@"HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1),
            new(
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\HandwritingErrorReports",
                "PreventHandwritingErrorReports",
                1
            ),
        ];

        protected override ServiceItem[] Services { get; } = [ServiceItem.Disable("WerSvc")];

        protected override string[] ScheduledTasks { get; } =
        [
            @"\Microsoft\Windows\Windows Error Reporting\QueueReporting",
            @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector",
            @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticResolver",
            @"\Microsoft\Windows\Diagnosis\Scheduled",
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Privacy),
                    Improves(ImpactArea.Performance),
                    Costs(ImpactArea.Features),
                ],
                Sources =
                [
                    Source(
                        "Windows Error Reporting - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/win32/wer/windows-error-reporting"
                    ),
                ],
            };
    }

    [Optimization(
        Id = "3F7C1E92-4A8B-4D6E-91F5-8C2A6E4D7B05",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.System | OptimizationTags.Performance
    )]
    public class DisableCompatibilityAssistant : DeclarativeOptimization
    {
        protected override ServiceItem[] Services { get; } = [ServiceItem.Disable("PcaSvc")];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Performance), Costs(ImpactArea.Features)],
                Sources =
                [
                    Source(
                        "Guidance on configuring system services (PcaSvc) - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/iot/iot-enterprise/optimize/services#system-services"
                    ),
                    Source(
                        "Program Compatibility Assistant Service - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows-server/security/windows-services/security-guidelines-for-disabling-system-services-in-windows-server#program-compatibility-assistant-service"
                    ),
                ],
                RequiresRestart = RestartRequirement.Reboot,
            };
    }

    [Optimization(
        Id = "B1C2D3E4-F5A6-4B7C-8D9E-0F1A2B3C4D5E",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Privacy | OptimizationTags.System
    )]
    public class DisableAdvertisingAndSuggestions : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0),
            new(
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo",
                "DisabledByGroupPolicy",
                1
            ),
            new(
                @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy",
                "TailoredExperiencesWithDiagnosticDataEnabled",
                0
            ),
            new(
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent",
                "DisableTailoredExperiencesWithDiagnosticData",
                1
            ),
            new(
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent",
                "DisableWindowsConsumerFeatures",
                1
            ),
            new(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableSoftLanding", 1),
            new(
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent",
                "DisableThirdPartySuggestions",
                1
            ),
            new(
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement",
                "ScoobeSystemSettingEnabled",
                0
            ),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Privacy), Improves(ImpactArea.Clutter)],
                Sources =
                [
                    Source(
                        "Manage connections to Microsoft services - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/privacy/manage-connections-from-windows-operating-system-components-to-microsoft-services"
                    ),
                ],
            };
    }

    [Optimization(
        Id = "8A2E5D1F-9C4B-47E3-B6A1-4F8C2E9D3A06",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Privacy | OptimizationTags.System
    )]
    public class DisableTypingAndInkingPersonalization : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(
                @"HKCU\SOFTWARE\Microsoft\InputPersonalization",
                "RestrictImplicitInkCollection",
                1
            ),
            new(
                @"HKCU\SOFTWARE\Microsoft\InputPersonalization",
                "RestrictImplicitTextCollection",
                1
            ),
            new(
                @"HKCU\SOFTWARE\Microsoft\InputPersonalization\TrainedDataStore",
                "HarvestContacts",
                0
            ),
            new(@"HKCU\Control Panel\International\User Profile", "HttpAcceptLanguageOptOut", 1),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Privacy), Costs(ImpactArea.Features)],
                Sources =
                [
                    Source(
                        "Manage connections to Microsoft services (Inking & Typing) - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/privacy/manage-connections-from-windows-operating-system-components-to-microsoft-services#1821-inking--typing"
                    ),
                    Source(
                        "Windows Privacy Compliance Guide - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/privacy/windows-privacy-compliance-guide"
                    ),
                ],
            };
    }

    [Optimization(
        Id = "6D217B90-0957-4861-B3F5-58ECF3E236EE",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Privacy | OptimizationTags.Windows10Only | OptimizationTags.System,
        Condition = typeof(Windows10Condition)
    )]
    public class HideMeetNowButton : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                "HideSCAMeetNow",
                1
            ),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Clutter)],
                Sources =
                [
                    Source(
                        "Taskbar Policy Settings (Remove Meet Now) - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/configuration/taskbar/policy-settings"
                    ),
                ],
                RequiresRestart = RestartRequirement.Explorer,
            };
    }

    [Optimization(
        Id = "A1B2C3D4-E5F6-4A7B-8C9D-0E1F2A3B4C5D",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Privacy | OptimizationTags.System
    )]
    public class DisableActivityHistory : DeclarativeOptimization
    {
        // EnableActivityFeed = 0 is intentionally omitted because it also disables
        // Windows 10/11 Clipboard History (Win+V).
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0),
            new(
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\System",
                "PublishUserActivitiesOnUserConsent",
                0
            ),
            new(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Privacy)],
                Sources =
                [
                    Source(
                        "PublishUserActivities Privacy Policy CSP - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-privacy#publishuseractivities"
                    ),
                ],
            };
    }

    [Optimization(
        Id = "C9E4F2A1-7B3D-48E5-9A6C-2D1F8E3B5A03",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Privacy | OptimizationTags.System
    )]
    public class DisableLocationTracking : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors",
                "DisableLocation",
                1
            ),
            new(@"HKLM\SYSTEM\Maps", "AutoUpdateEnabled", 0),
            new(
                @"HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Sensor\Permissions\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}",
                "SensorPermissionState",
                0
            ),
            new(
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location",
                "Value",
                "Deny"
            ),
            new(
                @"HKLM\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location",
                "Value",
                "Deny"
            ),
            new(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Geolocation", "Status", 0),
            new(@"HKLM\SYSTEM\CurrentControlSet\Services\lfsvc\Service\Configuration", "Status", 0),
            new(
                @"HKLM\Software\Microsoft\PolicyManager\default\WiFi\AllowWiFiHotSpotReporting",
                "Value",
                0
            ),
            new(
                @"HKLM\Software\Microsoft\PolicyManager\default\WiFi\AllowAutoConnectToWiFiSenseHotspots",
                "Value",
                0
            ),
        ];

        protected override ServiceItem[] Services { get; } =
        [ServiceItem.Disable("lfsvc"), ServiceItem.Manual("MapsBroker")];

        protected override string[] ScheduledTasks { get; } =
        [@"\Microsoft\Windows\Maps\MapsUpdateTask"];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Privacy),
                    Improves(ImpactArea.Power),
                    Costs(ImpactArea.Features),
                ],
                Sources =
                [
                    Source(
                        "ADMX_Sensors Policy CSP (DisableLocation) - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-admx-sensors#disablelocation_1"
                    ),
                    Source(
                        "AllowLocation System Policy CSP - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-system#allowlocation"
                    ),
                ],
            };
    }

    [Optimization(
        Id = "A5F8C3D2-6E1B-49A7-8C4E-5B2D9F1A6C04",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Privacy | OptimizationTags.System
    )]
    public class DisableHardwareSensors : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [new(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableSensors", 1)];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Privacy), Costs(ImpactArea.Features)],
                Sources =
                [
                    Source(
                        "ADMX_Sensors Policy CSP (DisableSensors) - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-admx-sensors#disablesensors_2"
                    ),
                ],
            };
    }

    [Optimization(
        Id = "4107430C-0074-4380-90E7-3662572E4720",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Privacy | OptimizationTags.System | OptimizationTags.Performance
    )]
    public class DisableAutoLogger : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\AppModel", "Start", 0),
            new(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\Cellcore", "Start", 0),
            new(
                @"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\CloudExperienceHostOobe",
                "Start",
                0
            ),
            new(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\DataMarket", "Start", 0),
            new(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\DiagLog", "Start", 0),
            new(
                @"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\Diagtrack-Listener",
                "Start",
                0
            ),
            new(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\LwtNetLog", "Start", 0),
            new(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\SQMLogger", "Start", 0),
            new(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\WdiContextLog", "Start", 0),
            new(@"HKLM\SYSTEM\CurrentControlSet\Control\WMI\Autologger\WiFiSession", "Start", 0),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Privacy),
                    Improves(ImpactArea.Performance),
                    Costs(ImpactArea.Features),
                ],
                Sources =
                [
                    Source(
                        "Configuring and Starting an AutoLogger Session - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/win32/etw/configuring-and-starting-an-autologger-session"
                    ),
                ],
                RequiresRestart = RestartRequirement.Reboot,
            };
    }

    [Optimization(
        Id = "6856782A-B530-4623-BD89-942D73FB82FD",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Privacy | OptimizationTags.System | OptimizationTags.Windows10Only,
        Condition = typeof(Windows10Condition)
    )]
    public class DisableCortana : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0),
            new(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCloudSearch", 0),
            new(
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search",
                "AllowCortanaAboveLock",
                0
            ),
            new(
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search",
                "AllowSearchToUseLocation",
                0
            ),
            new(
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search",
                "ConnectedSearchUseWeb",
                0
            ),
            new(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", "CortanaConsent", 0),
            new(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", "CortanaConsent2", 0),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Privacy), Improves(ImpactArea.Clutter)],
                Sources =
                [
                    Source(
                        "AllowCortana Experience Policy CSP - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-experience#allowcortana"
                    ),
                ],
                RequiresRestart = RestartRequirement.SignOut,
            };
    }

    [Optimization(
        Id = "00C997FE-1CB7-41BD-B473-65A81333AEE9",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Performance | OptimizationTags.Privacy
    )]
    public class DisableContentDeliveryManager : DeclarativeOptimization
    {
        private const string ContentDeliveryPath =
            @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";

        // SystemPaneSuggestionsEnabled is intentionally omitted here because it is
        // owned by Customize -> Preferences -> SystemSuggestions.
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(ContentDeliveryPath, "ContentDeliveryAllowed", 0),
            new(ContentDeliveryPath, "SubscribedContent-338387Enabled", 0),
            new(ContentDeliveryPath, "SubscribedContent-338388Enabled", 0),
            new(ContentDeliveryPath, "SubscribedContent-338389Enabled", 0),
            new(ContentDeliveryPath, "SubscribedContent-353698Enabled", 0),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Clutter), Improves(ImpactArea.Privacy)],
                Sources =
                [
                    Source(
                        "AllowWindowsSpotlight Experience Policy CSP - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-experience#allowwindowsspotlight"
                    ),
                ],
            };
    }

    [Optimization(
        Id = "A4823C34-0BDD-4418-83A0-4968102D1771",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Privacy | OptimizationTags.System
    )]
    public class DisableFindMyDevice : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [new(@"HKLM\SOFTWARE\Policies\Microsoft\FindMyDevice", "AllowFindMyDevice", 0)];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Privacy), Costs(ImpactArea.Security)],
                Sources =
                [
                    Source(
                        "AllowFindMyDevice Experience Policy CSP - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-experience#allowfindmydevice"
                    ),
                ],
            };
    }

    [Optimization(
        Id = "6F242AD6-4717-4161-A7BF-A2AF5F0C9BDD",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Network | OptimizationTags.Privacy
    )]
    public class DisableDeliveryOptimization : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization",
                "DODownloadMode",
                0
            ),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Network), Improves(ImpactArea.Privacy)],
                Sources =
                [
                    Source(
                        "Delivery Optimization Reference - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/deployment/do/waas-delivery-optimization-reference"
                    ),
                ],
            };
    }

    [Optimization(
        Id = "B8A9F7AE-A417-43E9-A4A7-DC4E839E3647",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.System | OptimizationTags.Security | OptimizationTags.Network
    )]
    public class ConfigureWindowsUpdate : BaseOptimization, IConfigurableOptimization
    {
        private sealed record UpdateOptionGroup(
            string Key,
            bool DefaultEnabled,
            RegistryItem[] Items
        );

        private static readonly UpdateOptionGroup[] OptionGroups =
        [
            new(
                "PreventAutoReboot",
                true,
                [
                    new(
                        @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                        "NoAutoRebootWithLoggedOnUsers",
                        1
                    ),
                ]
            ),
            new(
                "DisableUpdateAsap",
                true,
                [
                    new(
                        @"HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                        "IsContinuousInnovationOptedIn",
                        0
                    ),
                ]
            ),
            new(
                "DisableDriverUpdates",
                false,
                [
                    new(
                        @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                        "ExcludeWUDriversInQualityUpdate",
                        1
                    ),
                    new(
                        @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching",
                        "SearchOrderConfig",
                        3
                    ),
                    new(
                        @"HKLM\SOFTWARE\Microsoft\WindowsUpdate\UpdatePolicy\PolicyState",
                        "ExcludeWUDrivers",
                        1
                    ),
                    new(
                        @"HKLM\SOFTWARE\Policies\Microsoft\Windows\Device Metadata",
                        "PreventDeviceMetadataFromNetwork",
                        1
                    ),
                ]
            ),
            new(
                "DeferUpdates",
                false,
                [
                    new(
                        @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                        "DeferFeatureUpdates",
                        1
                    ),
                    new(
                        @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                        "DeferFeatureUpdatesPeriodInDays",
                        365
                    ),
                    new(
                        @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                        "DeferQualityUpdates",
                        1
                    ),
                    new(
                        @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                        "DeferQualityUpdatesPeriodInDays",
                        4
                    ),
                ]
            ),
        ];

        public bool RequiresAtLeastOneSelection => true;

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Stability), Costs(ImpactArea.Security)],
                Sources =
                [
                    Source(
                        "ExcludeWUDriversInQualityUpdate Update Policy CSP - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-update#excludewudriversinqualityupdate"
                    ),
                    Source(
                        "Manage Windows Update Policies - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/deployment/update/waas-manage-updates-wufb"
                    ),
                ],
                SetsGroupPolicy = true,
            };

        public IReadOnlyList<OptimizationOption> GetOptions(
            SystemInfo snapshot,
            IReadOnlyDictionary<string, string>? previousSelections = null
        ) =>
            [
                .. OptionGroups.Select(group =>
                    ToggleOption(
                        group.Key,
                        previousSelections.GetBool(group.Key, group.DefaultEnabled),
                        StateProbe.RegistryMatches(group.Items) == true
                    )
                ),
            ];

        public override Task<bool?> IsSystemAtTargetAsync(
            SystemInfo snapshot,
            IReadOnlyDictionary<string, string>? appliedOptions = null,
            CancellationToken cancellationToken = default
        )
        {
            if (appliedOptions is null)
                return Task.FromResult<bool?>(null);

            var selectedItems = OptionGroups
                .Where(g => appliedOptions.GetBool(g.Key))
                .SelectMany(g => g.Items)
                .ToArray();

            return Task.FromResult(
                selectedItems.Length == 0 ? null : StateProbe.RegistryMatches(selectedItems)
            );
        }

        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            var items = OptionGroups
                .Where(g => context.Options.GetBool(g.Key, g.DefaultEnabled))
                .SelectMany(g => g.Items)
                .ToArray();

            if (items.Length == 0)
            {
                context.Changes.AddSkip(
                    ServiceStrings.RegistryName,
                    Loc.Instance[$"{Prefix}.NoSelection"]
                );
                return Task.FromResult(context.Changes.ToApplyResult());
            }

            RegistryService.Write(context, items);
            context.Logger.LogInformation(
                "Configured Windows Update behavior with {Count} registry setting(s)",
                items.Length
            );
            return Task.FromResult(context.Changes.ToApplyResult());
        }
    }

    [Optimization(
        Id = "C03396D4-F790-4689-AB6D-CE83C8FE09D8",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Disk | OptimizationTags.Security | OptimizationTags.Windows11Only,
        Condition = typeof(Windows11Condition)
    )]
    public class DisableBitLockerAutoEncryption : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [new(@"HKLM\SYSTEM\CurrentControlSet\Control\BitLocker", "PreventDeviceEncryption", 1)];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Storage),
                    Improves(ImpactArea.Performance),
                    Costs(ImpactArea.Security),
                ],
                Sources =
                [
                    Source(
                        "PreventDeviceEncryption Unattend Setting - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows-hardware/customize/desktop/unattend/microsoft-windows-securestartup-filterdriver-preventdeviceencryption"
                    ),
                ],
            };
    }
}
