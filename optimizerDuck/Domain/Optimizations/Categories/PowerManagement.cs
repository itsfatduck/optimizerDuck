using System.IO;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Conditions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.UI.Pages.Optimize.Categories;

namespace optimizerDuck.Domain.Optimizations.Categories;

[OptimizationCategory(typeof(PowerManagementOptimizerPage))]
public class PowerManagement() : OptimizationCategoryBase(OptimizationCategoryOrder.Power)
{
    [Optimization(
        Id = "C7A97DDE-6631-48BF-A0A8-590D447A81AB",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.System | OptimizationTags.Power | OptimizationTags.Disk
    )]
    public class DisableHibernateAndFastStartup : BaseOptimization, IConfigurableOptimization
    {
        private const string OptionKeyMode = "Mode";
        internal const string ModeFastStartupOnly = "FastStartupOnly";
        internal const string ModeDisableAll = "DisableAll";

        private static readonly RegistryItem FastStartupItem = new(
            @"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power",
            "HiberbootEnabled",
            0
        );

        private static string DefaultModeFor(SystemInfo snapshot) =>
            snapshot.Windows.DeviceKind == DeviceKind.Laptop ? ModeFastStartupOnly : ModeDisableAll;

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Storage),
                    Improves(ImpactArea.Stability),
                    Costs(ImpactArea.Power),
                ],
                Sources =
                [
                    Source(
                        "Fast Startup update and shutdown behavior - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/troubleshoot/windows-client/deployment/updates-not-install-with-fast-startup"
                    ),
                    Source(
                        "Hiberboot Policy CSP - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-admx-wininit#hiberboot"
                    ),
                ],
                RequiresRestart = RestartRequirement.Reboot,
            };

        public IReadOnlyList<OptimizationOption> GetOptions(
            SystemInfo snapshot,
            IReadOnlyDictionary<string, string>? previousSelections = null
        ) =>
            [
                SingleChoiceOption(
                    OptionKeyMode,
                    previousSelections.GetString(OptionKeyMode, DefaultModeFor(snapshot)),
                    ModeFastStartupOnly,
                    ModeDisableAll
                ),
            ];

        public override Task<bool?> IsSystemAtTargetAsync(
            SystemInfo snapshot,
            IReadOnlyDictionary<string, string>? appliedOptions = null,
            CancellationToken cancellationToken = default
        )
        {
            var hiberPresent = HibernationService.IsHibernationFilePresent();
            if (appliedOptions?.GetString(OptionKeyMode) == ModeFastStartupOnly)
            {
                if (hiberPresent == false)
                    return Task.FromResult<bool?>(true);

                return Task.FromResult(StateProbe.RegistryMatches(FastStartupItem));
            }

            return Task.FromResult<bool?>(hiberPresent is { } present ? !present : null);
        }

        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            var mode = context.Options.GetString(OptionKeyMode, DefaultModeFor(context.Snapshot));

            if (mode == ModeFastStartupOnly)
            {
                RegistryService.Write(context, FastStartupItem);
                context.Logger.LogInformation("Disabled Fast Startup while keeping hibernation");
                return Task.FromResult(context.Changes.ToApplyResult());
            }

            var wasPresent = HibernationService.IsHibernationFilePresent();
            if (wasPresent is null)
                context.Logger.LogWarning(
                    "Hibernation state could not be read, the recorded step will mark it unknown"
                );

            Apply(context, wasPresent);

            return Task.FromResult(context.Changes.ToApplyResult());
        }

        /// <summary>
        ///     Whether the native call is needed. An unknown state is attempted, because a failed
        ///     read is not the same as "already disabled".
        /// </summary>
        internal static bool NeedsHibernationChange(bool? wasPresent) => wasPresent is not false;

        internal static OpResult Apply(OpCall call, bool? wasPresent)
        {
            if (!NeedsHibernationChange(wasPresent))
            {
                call.Changes.AddSkip(
                    ServiceStrings.HibernationName,
                    ServiceStrings.HibernationDescriptionDisable,
                    new HibernationDetail { PreviousPresent = false, NewPresent = null }
                );
                call.Logger.LogInformation("Hibernation already disabled, skipping");
                return OpResult.Success();
            }

            var result = HibernationService.SetHibernationFile(present: false);
            var revert = new HibernationRevertStep { WasPresent = wasPresent };

            if (result.Succeeded)
            {
                call.Changes.Add(
                    ServiceStrings.HibernationName,
                    ServiceStrings.HibernationDescriptionDisable,
                    true,
                    revert,
                    detail: new HibernationDetail
                    {
                        PreviousPresent = wasPresent,
                        NewPresent = false,
                    }
                );
                call.Logger.LogInformation(
                    "Disabled hibernation and Fast Startup. Previous state: {State}",
                    wasPresent is { } present
                        ? present
                            ? "Enabled"
                            : "Disabled"
                        : "Unknown"
                );
                return OpResult.Success(revert);
            }

            var error = ServiceStrings.Format(
                ServiceStrings.HibernationErrorChangeFailed,
                $"0x{result.NativeStatus:X8}"
            );
            call.Logger.LogWarning(
                "[HIBERNATION][FAIL] NTSTATUS 0x{Status:X8}",
                result.NativeStatus
            );
            call.Changes.Add(
                ServiceStrings.HibernationName,
                ServiceStrings.HibernationDescriptionDisable,
                false,
                null,
                error,
                result.ExceptionText,
                retryCall => Task.FromResult(Apply(retryCall, wasPresent))
            );
            return OpResult.Fail(error, result.ExceptionText);
        }
    }

    [Optimization(
        Id = "35EE4A79-9D2E-4D45-919E-2EE4922213E4",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Power | OptimizationTags.Network | OptimizationTags.Windows11Only,
        Condition = typeof(LaptopWindows11Condition)
    )]
    public class DisableModernStandbyNetworking : DeclarativeOptimization
    {
        private const string PolicyPath =
            @"HKLM\SOFTWARE\Policies\Microsoft\Power\PowerSettings\f15576e8-98b7-4186-b944-eafa664402d9";

        protected override RegistryItem[] RegistryItems { get; } =
        [new(PolicyPath, "ACSettingIndex", 0), new(PolicyPath, "DCSettingIndex", 0)];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Power),
                    Improves(ImpactArea.Privacy),
                    Costs(ImpactArea.Features),
                ],
                Sources =
                [
                    Source(
                        "ACConnectivityInStandby_2 Power Policy CSP - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-admx-power#acconnectivityinstandby_2"
                    ),
                    Source(
                        "Modern Standby Network Connectivity - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows-hardware/design/device-experiences/modern-standby-network-connectivity"
                    ),
                ],
            };
    }

    internal const string UsbReasonNoDevices = "usb.noDevices";

    [Optimization(
        Id = "805F993F-67F9-4F5A-8606-998EA9087CF0",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Latency | OptimizationTags.Performance
    )]
    public class DisableUSBPowerSaving : BaseOptimization
    {
        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Latency), Costs(ImpactArea.Power)],
                Sources =
                [
                    Source(
                        "USB Selective Suspend - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows-hardware/drivers/usbcon/usb-selective-suspend"
                    ),
                ],
            };

        public override Task<bool?> IsSystemAtTargetAsync(
            SystemInfo snapshot,
            IReadOnlyDictionary<string, string>? appliedOptions = null,
            CancellationToken cancellationToken = default
        )
        {
            var captured = UsbPowerService.Capture();
            if (captured.Count == 0)
                return Task.FromResult<bool?>(null);

            return Task.FromResult<bool?>(captured.All(static s => !s.Enable));
        }

        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            context.Logger.LogInformation("Saving current USB power state");
            var captured = UsbPowerService.Capture();
            if (captured.Count == 0)
            {
                context.Logger.LogInformation("No USB devices found, nothing to change");
                context.Changes.AddNotApplicable(
                    ServiceStrings.UsbPowerName,
                    ServiceStrings.UsbPowerInfoNoDevices,
                    new UsbPowerDetail { DeviceCount = 0, Reason = UsbReasonNoDevices }
                );
                return Task.FromResult(context.Changes.ToApplyResult());
            }

            context.Logger.LogInformation("Disabling USB power saving");
            var revertStep = new UsbPowerRevertStep
            {
                States = captured
                    .Select(static state => new UsbPowerRevertStep.DeviceState
                    {
                        InstanceName = state.InstanceName,
                        Enable = state.Enable,
                    })
                    .ToList(),
            };

            Apply(context, revertStep, UsbPowerService.Disable());

            return Task.FromResult(context.Changes.ToApplyResult());
        }

        internal static OpResult Apply(
            OpCall call,
            UsbPowerRevertStep revertStep,
            UsbPowerService.UsbPowerWriteResult? result
        )
        {
            var description = ServiceStrings.Format(
                ServiceStrings.UsbPowerDescriptionDisable,
                revertStep.States.Count
            );

            if (result is { FailedDevices.Count: > 0 })
            {
                var detail = string.Join(", ", result.FailedDevices);
                call.Logger.LogWarning(
                    "[USB][PARTIAL] {Count} device(s) changed, {Failed} refused: {Detail}",
                    result.ChangedCount,
                    result.FailedDevices.Count,
                    detail
                );
                call.Changes.Add(
                    ServiceStrings.UsbPowerName,
                    description,
                    false,
                    revertStep,
                    ServiceStrings.UsbPowerErrorChangeFailed,
                    detail,
                    retryCall =>
                        Task.FromResult(Apply(retryCall, revertStep, UsbPowerService.Disable()))
                );
                return OpResult.Fail(ServiceStrings.UsbPowerErrorChangeFailed, detail);
            }

            if (result is { ChangedCount: 0 })
            {
                call.Logger.LogInformation("[USB][SKIP] every device already at the target");
                call.Changes.AddSkip(
                    ServiceStrings.UsbPowerName,
                    ServiceStrings.UsbPowerInfoAlreadyConfigured,
                    new UsbPowerDetail { DeviceCount = 0 }
                );
                return OpResult.Success();
            }

            if (result is not null)
            {
                call.Changes.Add(
                    ServiceStrings.UsbPowerName,
                    description,
                    true,
                    revertStep,
                    detail: new UsbPowerDetail { DeviceCount = result.ChangedCount }
                );
                call.Logger.LogInformation(
                    "[USB][OK] power saving disabled, {Count} device(s) changed",
                    result.ChangedCount
                );
                return OpResult.Success(revertStep);
            }

            var error = ServiceStrings.UsbPowerErrorChangeFailed;
            call.Logger.LogWarning("[USB][FAIL] WMI write refused or unavailable");
            call.Changes.Add(
                ServiceStrings.UsbPowerName,
                description,
                false,
                null,
                error,
                null,
                retryCall =>
                    Task.FromResult(Apply(retryCall, revertStep, UsbPowerService.Disable()))
            );
            return OpResult.Fail(error, null);
        }
    }

    [Optimization(
        Id = "EE71E993-EE41-4449-8856-84B09B2B0C46",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Latency | OptimizationTags.Performance | OptimizationTags.Power
    )]
    public class InstallOptimizerDuckPowerPlan : BaseOptimization
    {
        private static readonly Guid OptimizerDuckPlanId = Guid.Parse(Shared.PowerPlanGUID);

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
                        "Processor Power Management Options - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows-hardware/customize/power-settings/configure-processor-power-management-options"
                    ),
                    Source(
                        "Static Configuration Options for Core Parking - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows-hardware/customize/power-settings/static-configuration-options-for-core-parking"
                    ),
                ],
            };

        public override Task<bool?> IsSystemAtTargetAsync(
            SystemInfo snapshot,
            IReadOnlyDictionary<string, string>? appliedOptions = null,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(StateProbe.ActivePowerSchemeIs(OptimizerDuckPlanId));
        }

        public override async Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            var powerPlanPath = Path.Combine(
                Shared.AssetsDirectory,
                "PowerPlans",
                "optimizerDuck.pow"
            );
            if (
                !EmbeddedResourceHelper.TryExtract(
                    "PowerPlans.optimizerDuck.pow",
                    powerPlanPath,
                    true
                )
            )
            {
                context.Logger.LogError("Failed to extract embedded power plan");
                return ApplyResult.False(
                    "Failed to extract optimizerDuck power plan from resources."
                );
            }

            context.Logger.LogInformation("Extracted power plan to {Path}", powerPlanPath);

            progress?.Report(
                new ProcessingProgress
                {
                    Message = Loc.Instance[$"{ProgressPrefix}.Importing"],
                    IsIndeterminate = true,
                }
            );

            var install = await PowerPlanChanges.InstallAsync(
                context,
                context.PowerPlans,
                powerPlanPath,
                OptimizerDuckPlanId,
                context.CancellationToken
            );
            var installResult = install.Result;
            var installedId = install.InstalledId;
            var previousId = install.PreviousId;
            if (!installResult.Ok)
            {
                context.Logger.LogError(
                    "Failed to install optimizerDuck power plan: {Error}",
                    installResult.Error ?? "unknown"
                );
                if (previousId is null)
                    return ApplyResult.False(Loc.Instance[$"{ErrorPrefix}.DetectActivePlanFailed"]);
                return ApplyResult.False(
                    Loc.Instance[
                        installedId is null
                            ? $"{ErrorPrefix}.ImportFailed"
                            : $"{ErrorPrefix}.ActivateFailed"
                    ]
                );
            }

            context.Logger.LogInformation("Installed optimizerDuck power plan successfully!");
            return context.Changes.ToApplyResult();
        }
    }

    [Optimization(
        Id = "D2392F86-2B35-4BA2-939B-6FF38EE18EE6",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Power | OptimizationTags.Performance | OptimizationTags.System
    )]
    public class DisablePowerSaving : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(
                @"HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling",
                "PowerThrottlingOff",
                1
            ),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Performance), Costs(ImpactArea.Power)],
                Sources =
                [
                    Source(
                        "PowerThrottlingTurnOff Power Policy CSP - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-admx-power#powerthrottlingturnoff"
                    ),
                ],
                RequiresRestart = RestartRequirement.Reboot,
            };
    }
}
