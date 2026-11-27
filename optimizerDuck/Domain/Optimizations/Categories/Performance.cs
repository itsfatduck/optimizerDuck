using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Common.Helpers;
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

[OptimizationCategory(typeof(PerformanceOptimizerPage))]
public class Performance() : OptimizationCategoryBase(OptimizationCategoryOrder.Performance)
{
    [Optimization(
        Id = "648EC19A-FDA5-4607-8A7C-148B8B05FB4C",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.System | OptimizationTags.Performance | OptimizationTags.Ram
    )]
    public class DisableBackgroundApps : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
            BuildRegistryItems(Shared.IsWindows11OrGreater);

        internal static RegistryItem[] BuildRegistryItems(bool isWindows11OrGreater)
        {
            List<RegistryItem> items =
            [
                new(
                    @"HKCU\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications",
                    "GlobalUserDisabled",
                    1
                ),
                new(
                    @"HKLM\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy",
                    "LetAppsRunInBackground",
                    2
                ),
            ];

            if (!isWindows11OrGreater)
            {
                items.Add(
                    new(
                        @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Search",
                        "BackgroundAppGlobalToggle",
                        0
                    )
                );
            }

            return [.. items];
        }

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Memory),
                    Improves(ImpactArea.Power),
                    Costs(ImpactArea.Features),
                ],
                Sources =
                [
                    Source(
                        "Enterprise Background Activity Controls - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/application-management/enterprise-background-activity-controls"
                    ),
                ],
                SetsGroupPolicy = true,
            };
    }

    [Optimization(
        Id = "CD436A05-51F1-46E9-B4DE-5262EE7F812A",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.System | OptimizationTags.Performance | OptimizationTags.Ram,
        Condition = typeof(TwelveGbMaxRamCondition)
    )]
    public class ConsolidateServiceHosts : BaseOptimization
    {
        private const string ControlPath = @"HKLM\SYSTEM\CurrentControlSet\Control";
        private const string ValueName = "SvcHostSplitThresholdInKB";

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Memory), Costs(ImpactArea.Stability)],
                Sources =
                [
                    Source(
                        "Service Host Grouping in Windows 10/11 - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/application-management/svchost-service-refactoring"
                    ),
                    Source(
                        "WinUtil PR #5170: SvcHost splitting scope",
                        "https://github.com/ChrisTitusTech/winutil/pull/5170"
                    ),
                ],
                RequiresRestart = RestartRequirement.Reboot,
            };

        public override Task<bool?> IsSystemAtTargetAsync(
            SystemInfo snapshot,
            IReadOnlyDictionary<string, string>? appliedOptions = null,
            CancellationToken cancellationToken = default
        )
        {
            if (snapshot.Memory.TotalKB <= 0)
                return Task.FromResult<bool?>(null);

            if (
                !RegistryService.TryReadValue(new RegistryItem(ControlPath, ValueName), out var raw)
            )
                return Task.FromResult<bool?>(null);

            if (raw is null)
                return Task.FromResult<bool?>(false);

            var thresholdKb = Convert.ToInt64(raw);
            return Task.FromResult<bool?>(thresholdKb >= snapshot.Memory.TotalKB);
        }

        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            if (context.Snapshot.Memory.TotalKB <= 0)
            {
                context.Logger.LogInformation(
                    "Invalid RAM value: {RamTotalKB}. Skipping...",
                    context.Snapshot.Memory.TotalKB
                );
                return Task.FromResult(
                    ApplyResult.False(
                        Loc.Instance[$"{ErrorPrefix}.InvalidRAM", context.Snapshot.Memory.TotalKB]
                    )
                );
            }

            RegistryService.Write(
                context,
                new RegistryItem(
                    ControlPath,
                    ValueName,
                    context.Snapshot.Memory.TotalKB,
                    RegistryValueKind.DWord
                )
            );
            context.Logger.LogInformation(
                "Consolidated service hosts with threshold: {ThresholdKB} KB",
                context.Snapshot.Memory.TotalKB
            );
            return Task.FromResult(context.Changes.ToApplyResult());
        }
    }

    [Optimization(
        Id = "C51E4187-BE49-4376-A97D-46C967A033B5",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Performance
    )]
    public class ProcessPriority : DeclarativeOptimization
    {
        // 38 (0x26) = Short, Variable, High foreground boost.
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(
                @"HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl",
                "Win32PrioritySeparation",
                38
            ),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Performance), Improves(ImpactArea.Latency)],
                Sources =
                [
                    Source(
                        "Scheduling Priorities - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/win32/procthread/scheduling-priorities"
                    ),
                ],
            };
    }

    [Optimization(
        Id = "FFB49D94-CCA9-4591-B329-6FDA3A2758F9",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.System
            | OptimizationTags.Performance
            | OptimizationTags.Latency
            | OptimizationTags.Audio
            | OptimizationTags.Display
    )]
    public class OptimizeMultimediaScheduler : DeclarativeOptimization
    {
        private const string SystemProfileKey =
            @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";

        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(SystemProfileKey, "NoLazyMode", 1),
            new(SystemProfileKey, "AlwaysOn", 1),
            new(
                SystemProfileKey,
                "NetworkThrottlingIndex",
                unchecked((int)0xFFFFFFFF),
                RegistryValueKind.DWord
            ),
            new(SystemProfileKey, "SystemResponsiveness", 10, RegistryValueKind.DWord),
            new($@"{SystemProfileKey}\Tasks\Games", "Priority", 2),
            new($@"{SystemProfileKey}\Tasks\Games", "Scheduling Category", "High"),
            new($@"{SystemProfileKey}\Tasks\Games", "SFIO Priority", "High"),
            new($@"{SystemProfileKey}\Tasks\Games", "GPU Priority", 8),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Latency),
                    Improves(ImpactArea.Performance),
                    Costs(ImpactArea.Power),
                ],
                Sources =
                [
                    Source(
                        "Multimedia Class Scheduler Service - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/win32/procthread/multimedia-class-scheduler-service"
                    ),
                ],
                RequiresRestart = RestartRequirement.Reboot,
            };
    }

    [Optimization(
        Id = "613FE85C-770D-441C-B97A-147B89B99028",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Latency | OptimizationTags.System | OptimizationTags.Performance
    )]
    public class KeyboardLatencyOptimization : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(@"HKCU\Control Panel\Keyboard", "KeyboardDelay", "0"),
            new(@"HKCU\Control Panel\Keyboard", "KeyboardSpeed", "31"),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Latency)],
                Sources =
                [
                    Source(
                        "SystemParametersInfoW (SPI_SETKEYBOARDDELAY & SPI_SETKEYBOARDSPEED) - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow"
                    ),
                ],
                RequiresRestart = RestartRequirement.SignOut,
            };
    }

    [Optimization(
        Id = "B7BB32F8-C756-47A4-83F2-F6E7EC7D45B8",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Latency | OptimizationTags.System
    )]
    public class DisableAccessibilityKeyboardHotkeys : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(@"HKCU\Control Panel\Accessibility\StickyKeys", "Flags", "26"),
            new(@"HKCU\Control Panel\Accessibility\Keyboard Response", "Flags", "2"),
            new(@"HKCU\Control Panel\Accessibility\ToggleKeys", "Flags", "34"),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Clutter), Costs(ImpactArea.Features)],
                Sources =
                [
                    Source(
                        "Make your keyboard easier to use - Microsoft Support",
                        "https://support.microsoft.com/en-us/accessibility/windows/make-your-mouse-keyboard-and-other-input-devices-easier-to-use"
                    ),
                ],
                RequiresRestart = RestartRequirement.SignOut,
            };
    }

    [Optimization(
        Id = "D5A2F7C4-1E8B-4C93-B6D0-8F41AE92C7E3",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Network | OptimizationTags.Performance
    )]
    public class LiftWebDavFileSizeLimit : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(
                @"HKLM\SYSTEM\CurrentControlSet\Services\WebClient\Parameters",
                "FileSizeLimitInBytes",
                unchecked((int)0xFFFFFFFF),
                RegistryValueKind.DWord
            ),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Network)],
                Sources =
                [
                    Source(
                        "WebClient FileSizeLimitInBytes (Error 0x800700DF) - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/troubleshoot/windows-client/networking/error-0x800700df-file-size-exceeds-limit-allowed"
                    ),
                ],
                RequiresRestart = RestartRequirement.Reboot,
            };
    }

    [Optimization(
        Id = "A4296AE9-C23A-417C-91A6-343FB7A087D9",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Performance | OptimizationTags.Disk
    )]
    public class DisableExplorerFolderDiscovery : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(
                @"HKCU\Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\Bags\AllFolders\Shell",
                "FolderType",
                "NotSpecified",
                RegistryValueKind.String
            ),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Performance), Costs(ImpactArea.Features)],
                Sources =
                [
                    Source(
                        "Disable Explorer Automatic Folder Discovery - WinUtil",
                        "https://winutil.christitus.com/code-reference/tweaks/essential-tweaks/disableexplorerautodiscovery/"
                    ),
                ],
                RequiresRestart = RestartRequirement.Explorer,
            };
    }

    [Optimization(
        Id = "9BFBA77A-86CA-4141-B186-D9C78AA46384",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Ram | OptimizationTags.Performance | OptimizationTags.Windows11Only,
        Condition = typeof(Windows11_24H2OrGreaterCondition)
    )]
    public class DisableSearchWebView2 : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(
                @"HKLM\SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\8\1694661260",
                "EnabledState",
                1
            ),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Memory),
                    Improves(ImpactArea.Performance),
                    Costs(ImpactArea.Stability),
                ],
                Sources =
                [
                    Source(
                        "Disable SearchHost WebView2 discussion (Feature ID 1694661260) - WinUtil",
                        "https://github.com/ChrisTitusTech/winutil/issues/4241"
                    ),
                    Source(
                        "SearchHost WebView2 RAM overhead discussion - Win11Debloat",
                        "https://github.com/Raphire/Win11Debloat/issues/403"
                    ),
                ],
                RequiresRestart = RestartRequirement.Reboot,
            };
    }

    [Optimization(
        Id = "9F0ACB64-FBC3-41C9-8A31-8B500DFB6395",
        Risk = OptimizationRisk.Risky,
        Tags = OptimizationTags.Performance
            | OptimizationTags.Security
            | OptimizationTags.Windows11Only,
        Condition = typeof(MemoryIntegritySupportedCondition)
    )]
    public class DisableMemoryIntegrityHvci : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(
                @"HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity",
                "Enabled",
                0
            ),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Performance), Costs(ImpactArea.Security)],
                Sources =
                [
                    Source(
                        "Options to optimize gaming performance in Windows 11 - Microsoft Support",
                        "https://support.microsoft.com/en-us/windows/options-to-optimize-gaming-performance-in-windows-11-a255f612-2949-4373-a566-ff6f3f474613"
                    ),
                    Source(
                        "Memory integrity enablement - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows-hardware/design/device-experiences/oem-hvci-enablement"
                    ),
                ],
                RequiresRestart = RestartRequirement.Reboot,
                ConfirmBeforeApply = true,
            };

        public override Task<bool?> IsSystemAtTargetAsync(
            SystemInfo snapshot,
            IReadOnlyDictionary<string, string>? appliedOptions = null,
            CancellationToken cancellationToken = default
        )
        {
            if (!RegistryService.TryReadValue(RegistryItems[0], out var actual))
                return Task.FromResult<bool?>(null);

            if (actual is not null)
                return Task.FromResult<bool?>(
                    RegistryValues.Equal(actual, RegistryItems[0].Value, RegistryItems[0].Kind)
                );

            return Task.FromResult<bool?>(
                snapshot.Security.MemoryIntegrity switch
                {
                    VbsState.Disabled => true,
                    VbsState.Enabled => false,
                    _ => null,
                }
            );
        }
    }

    [Optimization(
        Id = "5CA213A9-F37B-4099-B12B-CDD336749CD5",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Network | OptimizationTags.Latency | OptimizationTags.Power,
        Condition = typeof(PhysicalNicCondition)
    )]
    public class DisableNicPowerSaving : BaseOptimization
    {
        // 0x10 (wake/management disabled) | 0x08 (power saving disabled) = 24
        private const int DisablePowerSavingPnPCapabilities = 24;

        private static RegistryItem[] BuildItems(IReadOnlyList<string> adapterPaths) =>
            [
                .. adapterPaths.Select(static path => new RegistryItem(
                    path,
                    "PnPCapabilities",
                    DisablePowerSavingPnPCapabilities
                )),
            ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Network),
                    Improves(ImpactArea.Latency),
                    Costs(ImpactArea.Power),
                ],
                Sources =
                [
                    Source(
                        "Power management setting on a network adapter (PnPCapabilities) - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/troubleshoot/windows-client/networking/power-management-on-network-adapter"
                    ),
                ],
                RequiresRestart = RestartRequirement.Reboot,
            };

        public override Task<bool?> IsSystemAtTargetAsync(
            SystemInfo snapshot,
            IReadOnlyDictionary<string, string>? appliedOptions = null,
            CancellationToken cancellationToken = default
        )
        {
            var adapterPaths = PhysicalNicCondition.GetPhysicalAdapterClassPaths();
            if (adapterPaths.Count == 0)
                return Task.FromResult<bool?>(null);

            return Task.FromResult(StateProbe.RegistryMatches(BuildItems(adapterPaths)));
        }

        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            var adapterPaths = PhysicalNicCondition.GetPhysicalAdapterClassPaths();
            if (adapterPaths.Count == 0)
            {
                context.Changes.AddNotApplicable(
                    ServiceStrings.RegistryName,
                    Loc.Instance[$"{Prefix}.NoAdapters"]
                );
                return Task.FromResult(context.Changes.ToApplyResult());
            }

            var items = BuildItems(adapterPaths);
            RegistryService.Write(context, items);
            context.Logger.LogInformation(
                "Disabled NIC power saving for {Count} physical adapter(s)",
                items.Length
            );
            return Task.FromResult(context.Changes.ToApplyResult());
        }
    }
}
