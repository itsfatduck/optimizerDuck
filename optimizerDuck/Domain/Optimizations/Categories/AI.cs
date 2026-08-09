using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Conditions;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.Optimization.Providers;
using optimizerDuck.UI.Pages.Optimize.Categories;

namespace optimizerDuck.Domain.Optimizations.Categories;

[OptimizationCategory(typeof(AIOptimizerPage))]
public class AI : IOptimizationCategory
{
    public string Name => Loc.Instance[$"Optimizer.{nameof(AI)}"];
    public OptimizationCategoryOrder Order { get; init; } = OptimizationCategoryOrder.AI;
    public ObservableCollection<IOptimization> Optimizations { get; init; } = [];

    [Optimization(
        Id = "8462E718-42CA-4BE6-940C-3620BF52A533",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Privacy | OptimizationTags.System | OptimizationTags.Windows11Only,
        Condition = typeof(RecallInstalledCondition)
    )]
    public class DisableRecall : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            // DisableAIDataAnalysis stops new snapshots (and deletes existing ones);
            // TurnOffSavingSnapshots mirrors the documented "Turn off saving snapshots"
            // policy for broad build coverage; AllowRecallEnablement is device-scope only
            // and removes the Recall bits on Windows 11 24H2+ (Build 26100.3915+).
            RegistryService.Write(
                new RegistryItem(
                    @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsAI",
                    "DisableAIDataAnalysis",
                    1
                ),
                new RegistryItem(
                    @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsAI",
                    "AllowRecallEnablement",
                    0
                ),
                new RegistryItem(
                    @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsAI",
                    "TurnOffSavingSnapshots",
                    1
                ),
                new RegistryItem(
                    @"HKCU\Software\Policies\Microsoft\Windows\WindowsAI",
                    "DisableAIDataAnalysis",
                    1
                ),
                new RegistryItem(
                    @"HKCU\Software\Policies\Microsoft\Windows\WindowsAI",
                    "TurnOffSavingSnapshots",
                    1
                )
            );
            context.Logger.LogInformation("Disabled Windows Recall AI snapshots");
            return Task.FromResult(CompleteFromScope());
        }
    }

    [Optimization(
        Id = "962C9960-6BFA-4FC5-97D9-BFE772426E39",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Privacy | OptimizationTags.System | OptimizationTags.Windows11Only,
        Condition = typeof(Windows11_24H2OrGreaterCondition)
    )]
    public class DisableClickToDo : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            RegistryService.Write(
                new RegistryItem(
                    @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsAI",
                    "DisableClickToDo",
                    1
                ),
                new RegistryItem(
                    @"HKCU\Software\Policies\Microsoft\Windows\WindowsAI",
                    "DisableClickToDo",
                    1
                )
            );
            context.Logger.LogInformation("Disabled Click To Do AI overlay");
            return Task.FromResult(CompleteFromScope());
        }
    }
}
