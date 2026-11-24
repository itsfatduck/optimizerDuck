using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.UI.Pages.Optimize.Categories;

namespace optimizerDuck.Domain.Optimizations.Categories;

[OptimizationCategory(typeof(BloatwareAndServicesOptimizerPage))]
public class BloatwareAndServices()
    : OptimizationCategoryBase(OptimizationCategoryOrder.BloatwareAndServices)
{
    [Optimization(
        Id = "E3C970B0-129D-4354-BAE4-2732E5BACCF7",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Disk
    )]
    public class DisablePreinstalledApps : DeclarativeOptimization
    {
        private const string ContentDeliveryPath =
            @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";

        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(ContentDeliveryPath, "PreInstalledAppsEnabled", 0),
            new(ContentDeliveryPath, "PreInstalledAppsEverEnabled", 0),
            new(ContentDeliveryPath, "OemPreInstalledAppsEnabled", 0),
            new(ContentDeliveryPath, "SilentInstalledAppsEnabled", 0),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Clutter),
                    Improves(ImpactArea.Storage),
                    Improves(ImpactArea.Privacy),
                ],
                Sources =
                [
                    Source(
                        "Turn off Microsoft consumer experiences - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-experience#allowwindowsconsumerfeatures"
                    ),
                ],
            };
    }

    [Optimization(
        Id = "1D6DCDDC-B7E0-4999-8360-A8EAAD6E136C",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Performance | OptimizationTags.Ram
    )]
    public class DisableUnusedLegacyServices : DeclarativeOptimization
    {
        // Strictly DowngradeOnly and disjoint from Telemetry, Error Reporting, Location,
        // Search Indexing, and optional hardware features.
        protected override ServiceItem[] Services { get; } =
        [
            ServiceItem.Disable("Fax"),
            ServiceItem.Disable("RemoteRegistry"),
            ServiceItem.Disable("RetailDemo"),
            ServiceItem.Disable("AJRouter"),
            ServiceItem.Disable("shpamsvc"),
            ServiceItem.Disable("AppVClient"),
            ServiceItem.Disable("AssignedAccessManagerSvc"),
            ServiceItem.Disable("DialogBlockingService"),
            ServiceItem.Disable("UevAgentService"),
            ServiceItem.Disable("NetTcpPortSharing"),
            ServiceItem.Disable("RemoteAccess"),
            ServiceItem.Manual("CscService"),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Performance),
                    Improves(ImpactArea.Memory),
                    Improves(ImpactArea.Security),
                ],
                Sources =
                [
                    Source(
                        "Guidance on disabling Windows system services - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/iot/iot-enterprise/optimize/services"
                    ),
                ],
                RequiresRestart = RestartRequirement.Reboot,
            };
    }

    [Optimization(
        Id = "F85534F4-EDDF-471E-B8A4-7A174584B178",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.System | OptimizationTags.Performance | OptimizationTags.Ram
    )]
    public class DisableOptionalFeatureServices : BaseOptimization, IConfigurableOptimization
    {
        private sealed record FeatureServiceGroup(string Key, ServiceItem[] Services);

        private static readonly FeatureServiceGroup[] Groups =
        [
            new(
                "DisablePrinting",
                [ServiceItem.Disable("Spooler"), ServiceItem.Disable("PrintNotify")]
            ),
            new(
                "DisableBluetooth",
                [
                    ServiceItem.Disable("bthserv"),
                    ServiceItem.Disable("BthAvctpSvc"),
                    ServiceItem.Disable("BTAGService"),
                ]
            ),
            new(
                "DisableXbox",
                [
                    ServiceItem.Disable("XblAuthManager"),
                    ServiceItem.Disable("XblGameSave"),
                    ServiceItem.Disable("XboxGipSvc"),
                    ServiceItem.Disable("XboxNetApiSvc"),
                ]
            ),
            new(
                "DisableLanSharing",
                [
                    ServiceItem.Disable("LanmanServer"),
                    ServiceItem.Disable("FDResPub"),
                    ServiceItem.Disable("fdPHost"),
                    ServiceItem.Disable("SSDPSRV"),
                ]
            ),
            new("DisableBiometrics", [ServiceItem.Disable("WbioSrvc")]),
            new(
                "DisableRemoteDesktop",
                [
                    ServiceItem.Disable("TermService"),
                    ServiceItem.Disable("UmRdpService"),
                    ServiceItem.Disable("SessionEnv"),
                ]
            ),
        ];

        public bool RequiresAtLeastOneSelection => true;

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Memory),
                    Improves(ImpactArea.Performance),
                    Costs(ImpactArea.Features),
                ],
                Sources =
                [
                    Source(
                        "Guidance on disabling Windows system services - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/iot/iot-enterprise/optimize/services"
                    ),
                ],
                RequiresRestart = RestartRequirement.Reboot,
            };

        public IReadOnlyList<OptimizationOption> GetOptions(
            SystemInfo snapshot,
            IReadOnlyDictionary<string, string>? previousSelections = null
        ) =>
            [
                .. Groups.Select(group =>
                    ToggleOption(
                        group.Key,
                        previousSelections.GetBool(group.Key, false),
                        StateProbe.ServicesAtLeast(group.Services) == true
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

            var selectedServices = Groups
                .Where(g => appliedOptions.GetBool(g.Key))
                .SelectMany(g => g.Services)
                .ToArray();

            return Task.FromResult(
                selectedServices.Length == 0 ? null : StateProbe.ServicesAtLeast(selectedServices)
            );
        }

        public override async Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            var selectedServices = Groups
                .Where(g => context.Options.GetBool(g.Key))
                .SelectMany(g => g.Services)
                .ToList();

            if (selectedServices.Count == 0)
            {
                context.Changes.AddSkip(
                    ServiceStrings.ServiceName,
                    Loc.Instance[$"{Prefix}.NoSelection"]
                );
                return context.Changes.ToApplyResult();
            }

            await ServiceProcessService.ChangeServiceStartupTypeAsync(
                context,
                selectedServices,
                progress
            );
            context.Logger.LogInformation(
                "Configured {Count} optional feature services",
                selectedServices.Count
            );
            return context.Changes.ToApplyResult();
        }
    }

    [Optimization(
        Id = "A08AC843-C98F-497F-8ADD-47E8C14C20B2",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Disk | OptimizationTags.Performance | OptimizationTags.Ram
    )]
    public class DisableSearchIndexing : DeclarativeOptimization
    {
        protected override ServiceItem[] Services { get; } = [ServiceItem.Disable("WSearch")];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Storage),
                    Improves(ImpactArea.Performance),
                    Costs(ImpactArea.Features),
                ],
                Sources =
                [
                    Source(
                        "System Services: Windows Search (WSearch) - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/iot/iot-enterprise/optimize/services#system-services"
                    ),
                ],
            };
    }

    [Optimization(
        Id = "B6280756-2414-47F3-9B7A-DE3E2EE9A28D",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Ram | OptimizationTags.Power | OptimizationTags.System
    )]
    public class DisableEdgeBackground : DeclarativeOptimization
    {
        private const string EdgePolicyPath = @"HKLM\SOFTWARE\Policies\Microsoft\Edge";

        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(EdgePolicyPath, "StartupBoostEnabled", 0),
            new(EdgePolicyPath, "BackgroundModeEnabled", 0),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Memory), Improves(ImpactArea.Power)],
                Sources =
                [
                    Source(
                        "Microsoft Edge Startup Boost & Background Mode Policies - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies#startupboostenabled"
                    ),
                ],
            };
    }
}
