using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.Optimization;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Services.Managers;
using optimizerDuck.Services.OptimizationServices;
using optimizerDuck.UI.Views.Pages.Optimizations;

namespace optimizerDuck.Core.Optimizers;

[OptimizationCategory(typeof(UserExperienceOptimizerPage))]
public class UserExperience : IOptimizationCategory
{
    public string Name => Loc.Instance[$"Optimizer.{nameof(UserExperience)}"];
    public OptimizationCategoryOrder Order { get; init; } = OptimizationCategoryOrder.UserExperience;
    public ObservableCollection<IOptimization> Optimizations { get; init; } = [];

    [Optimization(
        Id = "9E2C4F7D-5A6B-4E9D-8C71-6E2F5A9C3314",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Performance | OptimizationTags.Latency)]
    public sealed class SpeedUpExplorerAndMenus : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress,
            OptimizationContext context)
        {
            RegistryService.Write(
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize",
                    "StartupDelayInMSec", 0),
                new RegistryItem(@"HKCU\Control Panel\Desktop",
                    "MenuShowDelay", "0")
            );
            context.Logger.LogInformation("Speeded up Explorer and menus");
            return Task.FromResult(ApplyResult.True());
        }
    }

    [Optimization(
        Id = "A3D1E8F2-7B4C-4A5D-9E62-8F3A1B7C4D59",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Performance | OptimizationTags.Visual)]
    public sealed class DisableVisualEffects : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress,
            OptimizationContext context)
        {
            RegistryService.Write(
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "TaskbarAnimations", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "ListviewShadow", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "EnableTransparency", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\DWM",
                    "EnableAeroPeek", 0)
            );
            context.Logger.LogInformation("Disabled visual effects for better performance");
            return Task.FromResult(ApplyResult.True());
        }
    }
}