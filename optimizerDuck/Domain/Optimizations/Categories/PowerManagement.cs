using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Managers;
using optimizerDuck.Services.OptimizationServices;
using optimizerDuck.UI.Pages.Optimizations;

namespace optimizerDuck.Domain.Optimizations.Categories;

[OptimizationCategory(typeof(PowerManagementOptimizerPage))]
public class PowerManagement : IOptimizationCategory
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
            var wasEnabled = RegistryService.Read<int>(new RegistryItem(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HibernateEnabled")) != 0;
            string revertCommand = wasEnabled
                ? "powercfg /h on"
                : "powercfg /h off";

            await ShellService.CMDAsync("powercfg /h off", revertCommand);


            context.Logger.LogInformation(
                "Disabled hibernation and Fast Startup. Previous state: {State}",
                wasEnabled ? "Enabled" : "Disabled"
            );
            return ApplyResult.True();
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
            var usbStates = await ShellService.PowerShellAsync(
                """
                $states = Get-CimInstance -Namespace root\wmi -ClassName MSPower_DeviceEnable -ErrorAction SilentlyContinue |
                Where-Object { $_.InstanceName -match 'USB\\ROOT' } |
                Select-Object InstanceName, Enable

                $states | ConvertTo-Json -Compress
                """
            );

            if (string.IsNullOrWhiteSpace(usbStates.Stdout))
            {
                context.Logger.LogInformation("No USB devices found, skipping");
                return ApplyResult.True();
            }

            context.Logger.LogInformation("Disabling USB power saving");
            await ShellService.PowerShellAsync(
                """
                $devices = Get-CimInstance -Namespace root\wmi -ClassName MSPower_DeviceEnable -ErrorAction SilentlyContinue |
                Where-Object { $_.InstanceName -match 'USB\\ROOT' }

                foreach ($d in $devices) {
                    if ($d.Enable -ne $false) {
                        Set-CimInstance -CimInstance $d -Property @{ Enable = $false } | Out-Null
                    }
                }
                """
            );

            return ApplyResult.True();
        }
    }

    [Optimization(
        Id = "EE71E993-EE41-4449-8856-84B09B2B0C46",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Latency
            | OptimizationTags.Performance
            | OptimizationTags.NetworkRequired
            | OptimizationTags.Power
    )]
    public class InstallOptimizerDuckPowerPlan : BaseOptimization
    {
        public override async Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            var activeQuery = await ShellService.CMDAsync(
                "powercfg /getactivescheme"
            );
            var match = Regex.Match(
                activeQuery.Stdout,
                @"Power Scheme GUID:\s*([a-fA-F0-9\-]{36})",
                RegexOptions.IgnoreCase
            );

            if (!match.Success)
            {
                context.Logger.LogError("Failed to detect current active power plan");
                return ApplyResult.False(Loc.Instance[$"{ErrorPrefix}.DetectActivePlanFailed"]);
            }

            var previousPlanGuid = match.Groups[1].Value;
            context.Logger.LogInformation("Current active power plan: {Guid}", previousPlanGuid);

            progress?.Report(
                new ProcessingProgress
                {
                    Message = Loc.Instance[$"{ProgressPrefix}.Downloading"],
                    IsIndeterminate = true,
                }
            );

            var (success, powerPlanPath) = await context.StreamService.TryDownloadAsync(
                Shared.PowerPlanUrl,
                "optimizerDuck.pow"
            );
            if (!success || string.IsNullOrWhiteSpace(powerPlanPath))
            {
                context.Logger.LogError("Failed to download optimizerDuck power plan");
                return ApplyResult.False(Loc.Instance[$"{ErrorPrefix}.DownloadFailed"]);
            }

            context.Logger.LogInformation(
                "Downloaded optimizerDuck power plan to {Path}",
                powerPlanPath
            );

            progress?.Report(
                new ProcessingProgress
                {
                    Message = Loc.Instance[$"{ProgressPrefix}.Importing"],
                    IsIndeterminate = true,
                }
            );

            var importResult = await ShellService.CMDAsync(
                $"powercfg /import \"{powerPlanPath}\" {Shared.PowerPlanGUID}",
                $"powercfg /delete {Shared.PowerPlanGUID}"
            );
            if (importResult.ExitCode != 0)
            {
                context.Logger.LogError(
                    "Failed to import optimizerDuck power plan: {Error}",
                    importResult.Stderr
                );
                return ApplyResult.False(Loc.Instance[$"{ErrorPrefix}.ImportFailed"]);
            }

            var setActiveResult = await ShellService.CMDAsync(
                $"powercfg /setactive {Shared.PowerPlanGUID}",
                $"powercfg /setactive {previousPlanGuid}"
            );
            if (setActiveResult.ExitCode != 0)
            {
                context.Logger.LogError(
                    "Failed to activate optimizerDuck power plan: {Error}",
                    setActiveResult.Stderr
                );
                return ApplyResult.False(Loc.Instance[$"{ErrorPrefix}.ActivateFailed"]);
            }

            context.Logger.LogInformation("Installed optimizerDuck power plan successfully!");
            return ApplyResult.True();
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
            return Task.FromResult(ApplyResult.True());
        }
    }

}
