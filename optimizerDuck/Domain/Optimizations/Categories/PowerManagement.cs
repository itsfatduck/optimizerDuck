using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
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
        public override async Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            var hibernateItem = new RegistryItem(
                @"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power",
                "HibernateEnabled"
            );

            // default to restoring hibernation on when the previous state is unknown.
            var wasEnabled = true;
            if (RegistryService.TryReadValue(hibernateItem, out var hibernateValue, context.Logger))
                wasEnabled = hibernateValue switch
                {
                    null => true,
                    int value => value != 0,
                    long value => value != 0,
                    string value => value != "0",
                    _ => true,
                };

            string revertCommand = wasEnabled ? "powercfg /h on" : "powercfg /h off";

            await context.Shell.CMDAsync("powercfg /h off", context, revertCommand);

            context.Logger.LogInformation(
                "Disabled hibernation and Fast Startup. Previous state: {State}",
                wasEnabled ? "Enabled" : "Disabled"
            );
            return context.Changes.ToApplyResult();
        }
    }

    [Optimization(
        Id = "805F993F-67F9-4F5A-8606-998EA9087CF0",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Latency | OptimizationTags.Performance
    )]
    public class DisableUSBPowerSaving : BaseOptimization
    {
        public override async Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            context.Logger.LogInformation("Saving current USB power state");
            var usbStates = await context.Shell.QueryPowerShellAsync(
                """
                $states = Get-CimInstance -Namespace root\wmi -ClassName MSPower_DeviceEnable -ErrorAction SilentlyContinue |
                Where-Object { $_.InstanceName -match 'USB\\ROOT' } |
                Select-Object InstanceName, Enable

                $states | ConvertTo-Json -Compress
                """,
                context.Logger
            );

            if (string.IsNullOrWhiteSpace(usbStates.Stdout))
            {
                context.Logger.LogInformation("No USB devices found, skipping");
                return ApplyResult.True();
            }

            var capturedStates = ParseUsbPowerStates(usbStates.Stdout);
            if (capturedStates.Count == 0)
            {
                context.Logger.LogInformation("No USB device states parsed, skipping");
                return ApplyResult.True();
            }

            context.Logger.LogInformation("Disabling USB power saving");
            var revertStep = new UsbPowerRevertStep { States = capturedStates };
            var disableOp = await context.Shell.PowerShellAsync(
                """
                $devices = Get-CimInstance -Namespace root\wmi -ClassName MSPower_DeviceEnable -ErrorAction SilentlyContinue |
                Where-Object { $_.InstanceName -match 'USB\\ROOT' }

                foreach ($d in $devices) {
                    if ($d.Enable -ne $false) {
                        Set-CimInstance -CimInstance $d -Property @{ Enable = $false } | Out-Null
                    }
                }
                """,
                context,
                revertStep
            );
            var ok = disableOp.Ok;

            return context.Changes.ToApplyResult();
        }

        private static List<UsbPowerRevertStep.DeviceState> ParseUsbPowerStates(string stdout)
        {
            try
            {
                var token = JToken.Parse(stdout.Trim());
                return token switch
                {
                    JArray array => array.ToObject<List<UsbPowerRevertStep.DeviceState>>() ?? [],
                    JObject obj => [obj.ToObject<UsbPowerRevertStep.DeviceState>()!],
                    _ => [],
                };
            }
            catch (JsonException)
            {
                return [];
            }
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
