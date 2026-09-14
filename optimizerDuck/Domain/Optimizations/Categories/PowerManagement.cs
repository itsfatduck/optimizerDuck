using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Domain.UI;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.UI.Pages.Optimize.Categories;

namespace optimizerDuck.Domain.Optimizations.Categories;

[OptimizationCategory(typeof(PowerManagementOptimizerPage))]
public class PowerManagement : LocalizedObject, IOptimizationCategory
{
    public string Name => Loc.Instance[$"Optimizer.{nameof(PowerManagement)}"];
    public OptimizationCategoryOrder Order { get; init; } = OptimizationCategoryOrder.Power;
    public ObservableCollection<IOptimization> Optimizations { get; init; } = [];

    [Optimization(
        Id = "C7A97DDE-6631-48BF-A0A8-590D447A81AB",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.System | OptimizationTags.Power | OptimizationTags.Performance
    )]
    public class DisableHibernateAndFastStartup : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            // An unreadable previous state is recorded as unknown, so a revert leaves the system
            // alone instead of guessing and committing a hibernation file that never existed.
            var wasPresent = HibernationService.IsHibernationFilePresent();
            if (wasPresent is null)
                context.Logger.LogWarning(
                    "Hibernation state could not be read, the recorded step will mark it unknown"
                );

            Apply(context, wasPresent);

            return Task.FromResult(context.Changes.ToApplyResult());
        }

        internal static OpResult Apply(OpCall call, bool? wasPresent)
        {
            var result = HibernationService.SetHibernationFile(present: false);
            var revert = new HibernationRevertStep { WasPresent = wasPresent };

            if (result.Succeeded)
            {
                call.Changes.Add(
                    ServiceStrings.HibernationName,
                    ServiceStrings.HibernationDescriptionDisable,
                    true,
                    revert
                );
                call.Logger.LogInformation(
                    "Disabled hibernation and Fast Startup. Previous state: {State}",
                    wasPresent is { } present ? present ? "Enabled" : "Disabled" : "Unknown"
                );
                return OpResult.Success(revert);
            }

            // A refused transition fails the same way on a retry (it needs privileges), but the
            // retry keeps the recorded step consistent with the other providers.
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
        Id = "805F993F-67F9-4F5A-8606-998EA9087CF0",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Latency | OptimizationTags.Performance
    )]
    public class DisableUSBPowerSaving : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            context.Logger.LogInformation("Saving current USB power state");
            var captured = UsbPowerService.Capture();
            if (captured.Count == 0)
            {
                context.Logger.LogInformation("No USB devices found, skipping");
                return Task.FromResult(ApplyResult.True());
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

            // Some devices refusing still leaves the others changed, so the recorded step keeps
            // their previous state and the failure names the devices that refused.
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

            if (result is not null)
            {
                call.Changes.Add(ServiceStrings.UsbPowerName, description, true, revertStep);
                call.Logger.LogInformation(
                    "[USB][OK] power saving disabled, {Count} device(s) changed",
                    result.ChangedCount
                );
                return OpResult.Success(revertStep);
            }

            // Null means the WMI query itself failed (class unavailable or the write was refused),
            // which is the one case the old exit-code check also treated as a failure.
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
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Latency | OptimizationTags.Performance | OptimizationTags.Power
    )]
    public class InstallOptimizerDuckPowerPlan : BaseOptimization
    {
        private static readonly Guid OptimizerDuckPlanId = Guid.Parse(Shared.PowerPlanGUID);

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
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Power | OptimizationTags.Performance | OptimizationTags.System
    )]
    public class DisablePowerSaving : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            RegistryService.Write(
                context,
                new RegistryItem(
                    @"HKLM\SYSTEM\CurrentControlSet\Control\USB\AutomaticSurpriseRemoval",
                    "AttemptRecoveryFromUsbPowerDrain",
                    0
                ),
                new RegistryItem(
                    @"HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling",
                    "PowerThrottlingOff",
                    1
                )
            );

            context.Logger.LogInformation("Disabled power saving features");
            return Task.FromResult(context.Changes.ToApplyResult());
        }
    }
}
