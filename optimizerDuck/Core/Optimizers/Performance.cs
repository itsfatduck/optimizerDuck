using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.Optimization;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Services;
using optimizerDuck.Services.Managers;
using optimizerDuck.Services.OptimizationServices;
using optimizerDuck.UI.Views.Pages.Optimizations;

namespace optimizerDuck.Core.Optimizers;

[OptimizationCategory(typeof(PerformanceOptimizerPage))]
public class Performance : IOptimizationCategory
{
    public string Name { get; init; } = Loc.Instance[$"Optimizer.{nameof(Performance)}"];
    public OptimizationCategoryOrder Order { get; init; } = OptimizationCategoryOrder.Performance;
    public ObservableCollection<IOptimization> Optimizations { get; init; } = [];

    [Optimization(Id = "648EC19A-FDA5-4607-8A7C-148B8B05FB4C", Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.System | OptimizationTags.Performance | OptimizationTags.Ram)]
    public class DisableBackgroundApps : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications",
                    "GlobalUserDisabled", 1),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle",
                    0)
            );
            context.Logger.LogInformation("Disabled background apps");
            return Task.FromResult(ApplyResult.True());
        }
    }
    
    [Optimization(Id = "CD436A05-51F1-46E9-B4DE-5262EE7F812A", Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.System | OptimizationTags.Performance)]
    public class SvcHostSplit : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            if (context.Snapshot.Ram.TotalKB <= 0)
            {
                context.Logger.LogInformation("Invalid RAM value: {RamTotalKB}. Skipping...", context.Snapshot.Ram.TotalKB);
                return Task.FromResult(ApplyResult.False(string.Format(Loc.Instance[$"{ErrorPrefix}.InvalidRAM"], context.Snapshot.Ram.TotalKB)));
            }

            RegistryService.Write(
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control", "SvcHostSplitThresholdInKB", context.Snapshot.Ram.TotalKB,
                    RegistryValueKind.DWord)
            );
            context.Logger.LogInformation("Enabled service host splitting with threshold: {ThresholdKB} KB", context.Snapshot.Ram.TotalKB);
            return Task.FromResult(ApplyResult.True());
        }
    }

    [Optimization(Id = "C51E4187-BE49-4376-A97D-46C967A033B5", Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Performance | OptimizationTags.Power)]
    public class ProcessPriority : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            /*
             ref: https://forums.blurbusters.com/viewtopic.php?t=8535
            42 Dec, 2A Hex = Short, Fixed , High foreground boost.
            41 Dec, 29 Hex = Short, Fixed , Medium foreground boost.
            40 Dec, 28 Hex = Short, Fixed , No foreground boost.

            38 Dec, 26 Hex = Short, Variable , High foreground boost.
            37 Dec, 25 Hex = Short, Variable , Medium foreground boost.
            36 Dec, 24 Hex = Short, Variable , No foreground boost.

            26 Dec, 1A Hex = Long, Fixed, High foreground boost.
            25 Dec, 19 Hex = Long, Fixed, Medium foreground boost.
            24 Dec, 18 Hex = Long, Fixed, No foreground boost.

            22 Dec, 16 Hex = Long, Variable, High foreground boost.
            21 Dec, 15 Hex = Long, Variable, Medium foreground boost.
            20 Dec, 14 Hex = Long, Variable, No foreground boost.
             */

            const int win32Priority = 38; // Short, Variable, High foreground boost


            RegistryService.Write(
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation",
                    win32Priority)
            );
            context.Logger.LogInformation("Enabled foreground boost with priority: {Priority}", win32Priority);
            return Task.FromResult(ApplyResult.True());
        }
    }

    [Optimization(Id = "D3E93D47-FC7E-44E2-8B58-D8626A75DB93", Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Performance | OptimizationTags.Power)]
    public class GameTaskScheduling : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new RegistryItem(
                    @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                    "Priority", 2),
                new RegistryItem(
                    @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                    "Scheduling Category", "High"),
                new RegistryItem(
                    @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                    "SFIO Priority", "High"),
                new RegistryItem(
                    @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                    "GPU Priority", 8)
            );

            context.Logger.LogInformation("Enabled game task scheduling with high priority");
            return Task.FromResult(ApplyResult.True());
        }
    }

    [Optimization(
        Id = "FFB49D94-CCA9-4591-B329-6FDA3A2758F9",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Performance | OptimizationTags.Latency)]
    public class MultimediaResponsiveness : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            const string systemProfileKey =
                @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";

            RegistryService.Write(
                new RegistryItem(
                    systemProfileKey,
                    "NetworkThrottlingIndex",
                    unchecked((int)0xFFFFFFFF),
                    RegistryValueKind.DWord),
                new RegistryItem(systemProfileKey, "SystemResponsiveness",
                    10) // minimum possible value (Values below 10 and above 100 are clamped to 20.)
            );

            context.Logger.LogInformation("Optimized multimedia responsiveness for low latency");
            return Task.FromResult(ApplyResult.True());
        }
    }


    [Optimization(
        Id = "70D84D83-01DB-455A-8004-D80BC372094C",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.System | OptimizationTags.Performance | OptimizationTags.Latency)]
    public class DisableGameBar : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new RegistryItem(@"HKCU\System\GameConfigStore", "GameBarEnabled", 0),

                new RegistryItem(@"HKCU\Software\Microsoft\GameBar", "ShowStartupPanel", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\GameBar", "UseNexusForGameBarEnabled", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\GameBar", "GamePanelStartupTipIndex", 0)
            );
            context.Logger.LogInformation("Disabled Xbox Game Bar");
            return Task.FromResult(ApplyResult.True());
        }
    }

    [Optimization(
        Id = "B453F151-7408-49FD-8898-F810E7E45902",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Performance | OptimizationTags.Latency |
               OptimizationTags.Power)]
    public class EnableGameMode : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new RegistryItem(@"HKCU\Software\Microsoft\GameBar", "AllowAutoGameMode", 1),

                new RegistryItem(@"HKCU\Software\Microsoft\GameBar", "AutoGameModeEnabled", 1)
            );
            context.Logger.LogInformation("Enabled Windows Game Mode");
            return Task.FromResult(ApplyResult.True());
        }
    }

    [Optimization(
        Id = "E6DBF550-0191-49D2-AEED-3F80A11592EE",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.System | OptimizationTags.Performance | OptimizationTags.Latency |
               OptimizationTags.Power)]
    public class DisableGameDVR : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0),

                new RegistryItem(@"HKCU\System\GameConfigStore", "GameDVR_Enabled", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Microsoft\PolicyManager\default\ApplicationManagement\AllowGameDVR",
                    "value", 0)
            );
            context.Logger.LogInformation("Disabled Game DVR recording");
            return Task.FromResult(ApplyResult.True());
        }
    }

    [Optimization(
        Id = "7D386B99-CDAD-42C9-924E-84307AA683AD",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Latency)]
    public class DisableMouseAcceleration : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new RegistryItem(@"HKCU\Control Panel\Mouse", "MouseSpeed", "0"),

                new RegistryItem(@"HKCU\Control Panel\Mouse", "MouseThreshold1", "0"),
                new RegistryItem(@"HKCU\Control Panel\Mouse", "MouseThreshold2", "0"),
                new RegistryItem(@"HKCU\Control Panel\Mouse", "MouseSensitivity", "10")
            );
            context.Logger.LogInformation("Disabled mouse acceleration");
            return Task.FromResult(ApplyResult.True());
        }
    }

    [Optimization(
        Id = "613FE85C-770D-441C-B97A-147B89B99028",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Latency)]
    public class KeyboardLatencyOptimization : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new RegistryItem(@"HKEY_CURRENT_USER\Control Panel\Keyboard", "KeyboardDelay", "0"),

                new RegistryItem(@"HKEY_CURRENT_USER\Control Panel\Keyboard", "KeyboardSpeed", "31"),
                new RegistryItem(@"HKEY_CURRENT_USER\Control Panel\Accessibility\StickyKeys", "Flags", "506"),
                new RegistryItem(@"HKEY_CURRENT_USER\Control Panel\Accessibility\Keyboard Response", "Flags", "122"),
                new RegistryItem(@"HKEY_CURRENT_USER\Control Panel\Accessibility\ToggleKeys", "Flags", "58")
            );

            context.Logger.LogInformation("Optimized keyboard responsiveness");
            return Task.FromResult(ApplyResult.True());
        }
    }
}