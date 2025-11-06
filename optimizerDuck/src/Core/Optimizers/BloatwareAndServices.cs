using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Managers;
using optimizerDuck.Core.Services;
using optimizerDuck.Interfaces;
using optimizerDuck.Models;
using optimizerDuck.UI;
using optimizerDuck.UI.Logger;

namespace optimizerDuck.Core.Optimizers;

public class BloatwareAndServices : IOptimizationGroup
{
    public string Name { get; } = "Bloatware & Services";
    public int Order { get; } = (int)OptimizationGroupOrder.BloatwareAndServices;
    public static ILogger Log { get; } = Logger.CreateLogger<BloatwareAndServices>();

    public class RemoveBloatwareApps : IOptimizationTweak
    {
        public string Name { get; } = "Remove Bloatware Apps";

        public string Description { get; } =
            "Removes selected bloatware apps (after continue) and bloatware that take up storage space and system resources";

        public bool EnabledByDefault { get; } = true;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin();

            while (OptimizationManager.SelectedBloatware.TryDequeue(out var appxPackage))
            {
                appxPackage = appxPackage with { DisplayName = appxPackage.DisplayName.TrimEnd(), Version = appxPackage.Version.TrimEnd() };
                Log.LogInformation($"Removing bloatware app: [{Theme.Primary}]{appxPackage.DisplayName}[/] [{Theme.Success}]{appxPackage.Version}[/] ([dim]{appxPackage.Name}[/])");

                ShellService.PowerShell($$"""
                                               $packages = Get-AppxPackage -AllUsers | Where-Object { $_.Name -eq "{{appxPackage.Name}}" }
                                               Write-Output "Found $($packages.Count) packages for {{appxPackage.Name}}"
                                               foreach ($pkg in $packages) {
                                                   Write-Output "Removing $($pkg.PackageFullName)"
                                                   Remove-AppxPackage -Package $pkg.PackageFullName -AllUsers
                                               }
                                               """);
            }

            Log.LogInformation("Bloatware apps have been removed.");

            RegistryService.Write(
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                    "PreInstalledAppsEnabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                    "SilentInstalledAppsEnabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                    "OemPreInstalledAppsEnabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                    "ContentDeliveryAllowed", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                    "SubscribedContentEnabled", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                    "PreInstalledAppsEverEnabled", 0)
            );
            Log.LogInformation("Preinstalled apps have been disabled.");

            return Task.CompletedTask;
        }
    }

    public class RemoveMicrosoftEdge : IOptimizationTweak
    {
        public string Name { get; } = "Debloat Microsoft Edge";

        public string Description { get; } =
            "Removes unnecessary Microsoft Edge features and reduces its system integration for improved performance";

        public bool EnabledByDefault { get; } = false;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin();
            RegistryService.Write(
                // thank you again, WinUtil
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\EdgeUpdate", "CreateDesktopShortcutDefault", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Edge", "PersonalizationReportingEnabled", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Edge", "ShowRecommendationsEnabled", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Edge", "HideFirstRunExperience", 1),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Edge", "UserFeedbackAllowed", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Edge", "ConfigureDoNotTrack", 1),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Edge", "AlternateErrorPagesEnabled", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Edge", "EdgeCollectionsEnabled", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Edge", "EdgeShoppingAssistantEnabled", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Edge", "MicrosoftEdgeInsiderPromotionEnabled", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Edge", "ShowMicrosoftRewards", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Edge", "WebWidgetAllowed", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Edge", "DiagnosticData", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Edge", "EdgeAssetDeliveryServiceEnabled", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Edge", "CryptoWalletEnabled", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Edge", "WalletDonationEnabled", 0)
            );
            Log.LogInformation("Microsoft Edge has been debloated.");
            return Task.CompletedTask;
        }
    }

    public class ConfigureServices : IOptimizationTweak
    {
        public string Name { get; } = "Configure Services";

        public string Description { get; } =
            "Optimizes Windows services by disabling unnecessary background services that consume system resources";

        public bool EnabledByDefault { get; } = true;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin();

            ServiceProcessService.ChangeServiceStartupType(
                new ServiceItem("AJRouter", ServiceStartupType.Disabled),
                new ServiceItem("ALG", ServiceStartupType.Manual),
                new ServiceItem("AppIDSvc", ServiceStartupType.Manual),
                new ServiceItem("AppMgmt", ServiceStartupType.Manual),
                new ServiceItem("AppReadiness", ServiceStartupType.Manual),
                new ServiceItem("AppVClient", ServiceStartupType.Disabled),
                new ServiceItem("AppXSvc", ServiceStartupType.Manual),
                new ServiceItem("Appinfo", ServiceStartupType.Manual),
                new ServiceItem("AssignedAccessManagerSvc", ServiceStartupType.Disabled),
                new ServiceItem("AudioEndpointBuilder", ServiceStartupType.Automatic),
                new ServiceItem("Audiosrv", ServiceStartupType.Automatic),
                new ServiceItem("AxInstSV", ServiceStartupType.Manual),
                new ServiceItem("BDESVC", ServiceStartupType.Manual),
                new ServiceItem("BFE", ServiceStartupType.Automatic),
                new ServiceItem("BITS", ServiceStartupType.Manual),
                new ServiceItem("BTAGService", ServiceStartupType.Manual),
                new ServiceItem("BrokerInfrastructure", ServiceStartupType.Automatic),
                new ServiceItem("Browser", ServiceStartupType.Manual),
                new ServiceItem("BthAvctpSvc", ServiceStartupType.Automatic),
                new ServiceItem("BthHFSrv", ServiceStartupType.Automatic),
                new ServiceItem("CDPSvc", ServiceStartupType.Manual),
                new ServiceItem("COMSysApp", ServiceStartupType.Manual),
                new ServiceItem("CertPropSvc", ServiceStartupType.Manual),
                new ServiceItem("ClipSVC", ServiceStartupType.Manual),
                new ServiceItem("CoreMessagingRegistrar", ServiceStartupType.Automatic),
                new ServiceItem("CryptSvc", ServiceStartupType.Automatic),
                new ServiceItem("CscService", ServiceStartupType.Manual),
                new ServiceItem("DPS", ServiceStartupType.Automatic),
                new ServiceItem("DcomLaunch", ServiceStartupType.Automatic),
                new ServiceItem("DcpSvc", ServiceStartupType.Disabled),
                new ServiceItem("DevQueryBroker", ServiceStartupType.Manual),
                new ServiceItem("DeviceAssociationService", ServiceStartupType.Manual),
                new ServiceItem("DeviceInstall", ServiceStartupType.Manual),
                new ServiceItem("Dhcp", ServiceStartupType.Automatic),
                new ServiceItem("DiagTrack", ServiceStartupType.Disabled),
                new ServiceItem("DialogBlockingService", ServiceStartupType.Disabled),
                new ServiceItem("DispBrokerDesktopSvc", ServiceStartupType.Automatic),
                new ServiceItem("DisplayEnhancementService", ServiceStartupType.Manual),
                new ServiceItem("DmEnrollmentSvc", ServiceStartupType.Manual),
                new ServiceItem("Dnscache", ServiceStartupType.Automatic),
                new ServiceItem("EFS", ServiceStartupType.Manual),
                new ServiceItem("EapHost", ServiceStartupType.Manual),
                new ServiceItem("EntAppSvc", ServiceStartupType.Manual),
                new ServiceItem("EventLog", ServiceStartupType.Automatic),
                new ServiceItem("EventSystem", ServiceStartupType.Automatic),
                new ServiceItem("FDResPub", ServiceStartupType.Manual),
                new ServiceItem("Fax", ServiceStartupType.Disabled),
                new ServiceItem("FontCache", ServiceStartupType.Automatic),
                new ServiceItem("FrameServer", ServiceStartupType.Manual),
                new ServiceItem("FrameServerMonitor", ServiceStartupType.Manual),
                new ServiceItem("GraphicsPerfSvc", ServiceStartupType.Manual),
                new ServiceItem("HomeGroupListener", ServiceStartupType.Disabled),
                new ServiceItem("HomeGroupProvider", ServiceStartupType.Disabled),
                new ServiceItem("HvHost", ServiceStartupType.Manual),
                new ServiceItem("IEEtwCollectorService", ServiceStartupType.Manual),
                new ServiceItem("IKEEXT", ServiceStartupType.Manual),
                new ServiceItem("InstallService", ServiceStartupType.Manual),
                new ServiceItem("InventorySvc", ServiceStartupType.Manual),
                new ServiceItem("IpxlatCfgSvc", ServiceStartupType.Manual),
                new ServiceItem("KeyIso", ServiceStartupType.Automatic),
                new ServiceItem("KtmRm", ServiceStartupType.Manual),
                new ServiceItem("LSM", ServiceStartupType.Automatic),
                new ServiceItem("LanmanServer", ServiceStartupType.Automatic),
                new ServiceItem("LanmanWorkstation", ServiceStartupType.Automatic),
                new ServiceItem("LicenseManager", ServiceStartupType.Manual),
                new ServiceItem("LxpSvc", ServiceStartupType.Manual),
                new ServiceItem("MSDTC", ServiceStartupType.Manual),
                new ServiceItem("MSiSCSI", ServiceStartupType.Manual),
                new ServiceItem("MapsBroker", ServiceStartupType.AutomaticDelayedStart),
                new ServiceItem("McpManagementService", ServiceStartupType.Manual),
                new ServiceItem("MicrosoftEdgeElevationService", ServiceStartupType.Manual),
                new ServiceItem("MixedRealityOpenXRSvc", ServiceStartupType.Manual),
                new ServiceItem("MpsSvc", ServiceStartupType.Automatic),
                new ServiceItem("MsKeyboardFilter", ServiceStartupType.Manual),
                new ServiceItem("NaturalAuthentication", ServiceStartupType.Manual),
                new ServiceItem("NcaSvc", ServiceStartupType.Manual),
                new ServiceItem("NcbService", ServiceStartupType.Manual),
                new ServiceItem("NcdAutoSetup", ServiceStartupType.Manual),
                new ServiceItem("NetSetupSvc", ServiceStartupType.Manual),
                new ServiceItem("NetTcpPortSharing", ServiceStartupType.Disabled),
                new ServiceItem("Netlogon", ServiceStartupType.Automatic),
                new ServiceItem("Netman", ServiceStartupType.Manual),
                new ServiceItem("NgcCtnrSvc", ServiceStartupType.Manual),
                new ServiceItem("NgcSvc", ServiceStartupType.Manual),
                new ServiceItem("NlaSvc", ServiceStartupType.Manual),
                new ServiceItem("PNRPAutoReg", ServiceStartupType.Manual),
                new ServiceItem("PNRPsvc", ServiceStartupType.Manual),
                new ServiceItem("PcaSvc", ServiceStartupType.Manual),
                new ServiceItem("PeerDistSvc", ServiceStartupType.Manual),
                new ServiceItem("PerfHost", ServiceStartupType.Manual),
                new ServiceItem("PhoneSvc", ServiceStartupType.Manual),
                new ServiceItem("PlugPlay", ServiceStartupType.Automatic),
                new ServiceItem("PolicyAgent", ServiceStartupType.Manual),
                new ServiceItem("Power", ServiceStartupType.Automatic),
                new ServiceItem("PrintNotify", ServiceStartupType.Disabled),
                new ServiceItem("ProfSvc", ServiceStartupType.Automatic),
                new ServiceItem("PushToInstall", ServiceStartupType.Manual),
                new ServiceItem("QWAVE", ServiceStartupType.Manual),
                new ServiceItem("RasAuto", ServiceStartupType.Manual),
                new ServiceItem("RasMan", ServiceStartupType.Manual),
                new ServiceItem("RemoteAccess", ServiceStartupType.Disabled),
                new ServiceItem("RemoteRegistry", ServiceStartupType.Disabled),
                new ServiceItem("RetailDemo", ServiceStartupType.Disabled),
                new ServiceItem("RmSvc", ServiceStartupType.Manual),
                new ServiceItem("RpcEptMapper", ServiceStartupType.Automatic),
                new ServiceItem("RpcLocator", ServiceStartupType.Manual),
                new ServiceItem("RpcSs", ServiceStartupType.Automatic),
                new ServiceItem("SCPolicySvc", ServiceStartupType.Manual),
                new ServiceItem("SCardSvr", ServiceStartupType.Manual),
                new ServiceItem("SDRSVC", ServiceStartupType.Manual),
                new ServiceItem("SEMgrSvc", ServiceStartupType.Manual),
                new ServiceItem("SENS", ServiceStartupType.Automatic),
                new ServiceItem("SNMPTRAP", ServiceStartupType.Manual),
                new ServiceItem("SSDPSRV", ServiceStartupType.Manual),
                new ServiceItem("SamSs", ServiceStartupType.Automatic),
                new ServiceItem("ScDeviceEnum", ServiceStartupType.Manual),
                new ServiceItem("Schedule", ServiceStartupType.Automatic),
                new ServiceItem("SecurityHealthService", ServiceStartupType.Manual),
                new ServiceItem("Sense", ServiceStartupType.Manual),
                new ServiceItem("SensorDataService", ServiceStartupType.Manual),
                new ServiceItem("SensorService", ServiceStartupType.Manual),
                new ServiceItem("SensrSvc", ServiceStartupType.Manual),
                new ServiceItem("SessionEnv", ServiceStartupType.Manual),
                new ServiceItem("SharedAccess", ServiceStartupType.Manual),
                new ServiceItem("SharedRealitySvc", ServiceStartupType.Manual),
                new ServiceItem("ShellHWDetection", ServiceStartupType.Automatic),
                new ServiceItem("SmsRouter", ServiceStartupType.Manual),
                new ServiceItem("Spooler", ServiceStartupType.Manual),
                new ServiceItem("SstpSvc", ServiceStartupType.Manual),
                new ServiceItem("StiSvc", ServiceStartupType.Manual),
                new ServiceItem("StorSvc", ServiceStartupType.Manual),
                new ServiceItem("SystemEventsBroker", ServiceStartupType.Automatic),
                new ServiceItem("TabletInputService", ServiceStartupType.Manual),
                new ServiceItem("TapiSrv", ServiceStartupType.Manual),
                new ServiceItem("TermService", ServiceStartupType.Automatic),
                new ServiceItem("Themes", ServiceStartupType.Automatic),
                new ServiceItem("TieringEngineService", ServiceStartupType.Manual),
                new ServiceItem("TimeBroker", ServiceStartupType.Manual),
                new ServiceItem("TimeBrokerSvc", ServiceStartupType.Manual),
                new ServiceItem("TokenBroker", ServiceStartupType.Manual),
                new ServiceItem("TrkWks", ServiceStartupType.Automatic),
                new ServiceItem("TroubleshootingSvc", ServiceStartupType.Manual),
                new ServiceItem("UI0Detect", ServiceStartupType.Manual),
                new ServiceItem("UevAgentService", ServiceStartupType.Disabled),
                new ServiceItem("UmRdpService", ServiceStartupType.Manual),
                new ServiceItem("UserManager", ServiceStartupType.Automatic),
                new ServiceItem("UsoSvc", ServiceStartupType.Manual),
                new ServiceItem("VGAuthService", ServiceStartupType.Automatic),
                new ServiceItem("VMTools", ServiceStartupType.Automatic),
                new ServiceItem("VSS", ServiceStartupType.Manual),
                new ServiceItem("VacSvc", ServiceStartupType.Manual),
                new ServiceItem("VaultSvc", ServiceStartupType.Automatic),
                new ServiceItem("W32Time", ServiceStartupType.Manual),
                new ServiceItem("WEPHOSTSVC", ServiceStartupType.Manual),
                new ServiceItem("WFDSConMgrSvc", ServiceStartupType.Manual),
                new ServiceItem("WMPNetworkSvc", ServiceStartupType.Disabled),
                new ServiceItem("WManSvc", ServiceStartupType.Manual),
                new ServiceItem("WPDBusEnum", ServiceStartupType.Manual),
                new ServiceItem("WSService", ServiceStartupType.Manual),
                new ServiceItem("WSearch", ServiceStartupType.Disabled),
                new ServiceItem("WaaSMedicSvc", ServiceStartupType.Manual),
                new ServiceItem("WalletService", ServiceStartupType.Manual),
                new ServiceItem("WarpJITSvc", ServiceStartupType.Manual),
                new ServiceItem("WbioSrvc", ServiceStartupType.Manual),
                new ServiceItem("Wcmsvc", ServiceStartupType.Automatic),
                new ServiceItem("WcsPlugInService", ServiceStartupType.Manual),
                new ServiceItem("WdNisSvc", ServiceStartupType.Manual),
                new ServiceItem("WdiServiceHost", ServiceStartupType.Manual),
                new ServiceItem("WdiSystemHost", ServiceStartupType.Manual),
                new ServiceItem("WebClient", ServiceStartupType.Manual),
                new ServiceItem("Wecsvc", ServiceStartupType.Manual),
                new ServiceItem("WerSvc", ServiceStartupType.Disabled),
                new ServiceItem("WiaRpc", ServiceStartupType.Manual),
                new ServiceItem("WinDefend", ServiceStartupType.Automatic),
                new ServiceItem("WinHttpAutoProxySvc", ServiceStartupType.Manual),
                new ServiceItem("WinRM", ServiceStartupType.Manual),
                new ServiceItem("Winmgmt", ServiceStartupType.Automatic),
                new ServiceItem("WlanSvc", ServiceStartupType.Automatic),
                new ServiceItem("WpcMonSvc", ServiceStartupType.Manual),
                new ServiceItem("WpnService", ServiceStartupType.Manual),
                new ServiceItem("XblAuthManager", ServiceStartupType.Manual),
                new ServiceItem("XblGameSave", ServiceStartupType.Manual),
                new ServiceItem("XboxGipSvc", ServiceStartupType.Manual),
                new ServiceItem("XboxNetApiSvc", ServiceStartupType.Manual),
                new ServiceItem("autotimesvc", ServiceStartupType.Manual),
                new ServiceItem("bthserv", ServiceStartupType.Manual),
                new ServiceItem("camsvc", ServiceStartupType.Manual),
                new ServiceItem("cloudidsvc", ServiceStartupType.Manual),
                new ServiceItem("dcsvc", ServiceStartupType.Manual),
                new ServiceItem("defragsvc", ServiceStartupType.Manual),
                new ServiceItem("diagnosticshub.standardcollector.service", ServiceStartupType.Disabled),
                new ServiceItem("diagsvc", ServiceStartupType.Manual),
                new ServiceItem("dmwappushservice", ServiceStartupType.Disabled),
                new ServiceItem("1394ohci", ServiceStartupType.Disabled),
                new ServiceItem("bowser", ServiceStartupType.Disabled),
                new ServiceItem("beep", ServiceStartupType.Disabled),
                new ServiceItem("dot3svc", ServiceStartupType.Manual),
                new ServiceItem("cdrom", ServiceStartupType.Manual),
                new ServiceItem("cdfs", ServiceStartupType.Manual),
                new ServiceItem("edgeupdate", ServiceStartupType.Manual),
                new ServiceItem("edgeupdatem", ServiceStartupType.Manual),
                new ServiceItem("embeddedmode", ServiceStartupType.Manual),
                new ServiceItem("fdPHost", ServiceStartupType.Manual),
                new ServiceItem("fhsvc", ServiceStartupType.Manual),
                new ServiceItem("gpsvc", ServiceStartupType.Automatic),
                new ServiceItem("hidserv", ServiceStartupType.Manual),
                new ServiceItem("icssvc", ServiceStartupType.Manual),
                new ServiceItem("iphlpsvc", ServiceStartupType.Automatic),
                new ServiceItem("lfsvc", ServiceStartupType.Manual),
                new ServiceItem("lltdsvc", ServiceStartupType.Manual),
                new ServiceItem("lmhosts", ServiceStartupType.Manual),
                new ServiceItem("msiserver", ServiceStartupType.Manual),
                new ServiceItem("netprofm", ServiceStartupType.Manual),
                new ServiceItem("nsi", ServiceStartupType.Automatic),
                new ServiceItem("p2pimsvc", ServiceStartupType.Manual),
                new ServiceItem("p2psvc", ServiceStartupType.Manual),
                new ServiceItem("perceptionsimulation", ServiceStartupType.Manual),
                new ServiceItem("pla", ServiceStartupType.Manual),
                new ServiceItem("seclogon", ServiceStartupType.Manual),
                new ServiceItem("shpamsvc", ServiceStartupType.Disabled),
                new ServiceItem("smphost", ServiceStartupType.Manual),
                new ServiceItem("spectrum", ServiceStartupType.Manual),
                new ServiceItem("sppsvc", ServiceStartupType.AutomaticDelayedStart),
                new ServiceItem("ssh-agent", ServiceStartupType.Disabled),
                new ServiceItem("svsvc", ServiceStartupType.Manual),
                new ServiceItem("swprv", ServiceStartupType.Manual),
                new ServiceItem("tiledatamodelsvc", ServiceStartupType.Automatic),
                new ServiceItem("tzautoupdate", ServiceStartupType.Disabled),
                new ServiceItem("uhssvc", ServiceStartupType.Disabled),
                new ServiceItem("upnphost", ServiceStartupType.Manual),
                new ServiceItem("vds", ServiceStartupType.Manual),
                new ServiceItem("vm3dservice", ServiceStartupType.Manual),
                new ServiceItem("vmicguestinterface", ServiceStartupType.Manual),
                new ServiceItem("vmicheartbeat", ServiceStartupType.Manual),
                new ServiceItem("vmickvpexchange", ServiceStartupType.Manual),
                new ServiceItem("vmicrdv", ServiceStartupType.Manual),
                new ServiceItem("vmicshutdown", ServiceStartupType.Manual),
                new ServiceItem("vmictimesync", ServiceStartupType.Manual),
                new ServiceItem("vmicvmsession", ServiceStartupType.Manual),
                new ServiceItem("vmicvss", ServiceStartupType.Manual),
                new ServiceItem("vmvss", ServiceStartupType.Manual),
                new ServiceItem("wbengine", ServiceStartupType.Manual),
                new ServiceItem("wcncsvc", ServiceStartupType.Manual),
                new ServiceItem("webthreatdefsvc", ServiceStartupType.Manual),
                new ServiceItem("wercplsupport", ServiceStartupType.Manual),
                new ServiceItem("wisvc", ServiceStartupType.Manual),
                new ServiceItem("wlidsvc", ServiceStartupType.Manual),
                new ServiceItem("wlpasvc", ServiceStartupType.Manual),
                new ServiceItem("wmiApSrv", ServiceStartupType.Manual),
                new ServiceItem("workfolderssvc", ServiceStartupType.Manual),
                new ServiceItem("wscsvc", ServiceStartupType.AutomaticDelayedStart),
                new ServiceItem("wuauserv", ServiceStartupType.Manual),
                new ServiceItem("wudfsvc", ServiceStartupType.Manual)
            );
            Log.LogInformation("Services have been configured.");

            var hasSystemSSD = s.Disk.Volumes.Any(volume => volume is { SystemDrive: true, DriveTypeDescription: "SSD" });

            ServiceProcessService.ChangeServiceStartupType(
                hasSystemSSD // Enable SysMain on SSD
                    ? new ServiceItem("SysMain", ServiceStartupType.Automatic)
                    : new ServiceItem("SysMain", ServiceStartupType.Disabled)
            );
            Log.LogDebug("System drive is {Type}, SysMain will be {Status}.", hasSystemSSD ? "SSD" : "HDD", hasSystemSSD ? "enabled" : "disabled");

            return Task.CompletedTask;
        }
    }
}