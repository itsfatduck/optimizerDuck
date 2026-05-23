using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Managers;
using optimizerDuck.Services.OptimizationServices;
using optimizerDuck.UI.Pages.Optimizations;

namespace optimizerDuck.Domain.Optimizations.Categories;

[OptimizationCategory(typeof(UserExperienceOptimizerPage))]
public class UserExperience : IOptimizationCategory
{
    public string Name => Loc.Instance[$"Optimizer.{nameof(UserExperience)}"];
    public OptimizationCategoryOrder Order { get; init; } =
        OptimizationCategoryOrder.UserExperience;
    public ObservableCollection<IOptimization> Optimizations { get; init; } = [];

    [Optimization(
        Id = "9E2C4F7D-5A6B-4E9D-8C71-6E2F5A9C3314",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Performance | OptimizationTags.Latency
    )]
    public sealed class SpeedUpExplorerAndMenus : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            RegistryService.Write(
                new RegistryItem(
                    @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize",
                    "StartupDelayInMSec",
                    0
                ),
                new RegistryItem(@"HKCU\Control Panel\Desktop", "MenuShowDelay", "0")
            );
            context.Logger.LogInformation("Speeded up Explorer and menus");
            return Task.FromResult(CompleteFromScope());
        }
    }

    [Optimization(
        Id = "A3D1E8F2-7B4C-4A5D-9E62-8F3A1B7C4D59",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Performance | OptimizationTags.Visual
    )]
    public sealed class DisableVisualEffects : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            RegistryService.Write(
                new RegistryItem(
                    @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "TaskbarAnimations",
                    0
                ),
                new RegistryItem(
                    @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "ListviewShadow",
                    0
                ),
                new RegistryItem(
                    @"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "EnableTransparency",
                    0
                ),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0)
            );
            context.Logger.LogInformation("Disabled visual effects for better performance");
            return Task.FromResult(CompleteFromScope());
        }
    }

    [Optimization(
        Id = "11111111-1111-1111-1111-111111111111",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System
    )]
    public sealed class TestPass : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            ExecutionScope.RecordStep("Test", "Step 1 — succeeds", true);
            ExecutionScope.RecordStep("Test", "Step 2 — succeeds", true);
            context.Logger.LogInformation("Test: all steps pass");
            return Task.FromResult(CompleteFromScope());
        }
    }

    [Optimization(
        Id = "22222222-2222-2222-2222-222222222222",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.System
    )]
    public sealed class TestAllFail : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            ExecutionScope.RecordStep("Test", "Step 1 — fails", false, null, "Something went wrong");
            ExecutionScope.RecordStep("Test", "Step 2 — also fails", false, null, "Access denied");
            context.Logger.LogInformation("Test: all steps fail");
            return Task.FromResult(CompleteFromScope());
        }
    }

    [Optimization(
        Id = "33333333-3333-3333-3333-333333333333",
        Risk = OptimizationRisk.Risky,
        Tags = OptimizationTags.System | OptimizationTags.Performance
    )]
    public sealed class TestPartialFail : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            ExecutionScope.RecordStep("Test", "Step 1 — succeeds", true);
            ExecutionScope.RecordStep("Test", "Step 2 — fails", false, null,
                "Something went wrong", null,
                "System.UnauthorizedAccessException: Access to the registry path is denied.\n   at Microsoft.Win32.RegistryKey.Win32Error(Int32 errorCode, String str)\n   at optimizerDuck.Services.OptimizationServices.RegistryService.Write(RegistryItem item)");
            ExecutionScope.RecordStep("Test", "Step 3 — succeeds", true);
            ExecutionScope.RecordStep("Test", "Step 4 — fails", false, null,
                "Access denied", null,
                "System.UnauthorizedAccessException: Access is not granted.\n   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)\n   at optimizerDuck.Services.OptimizationServices.RegistryService.DeleteValue(RegistryItem item)");
            context.Logger.LogInformation("Test: partial steps fail");
            return Task.FromResult(CompleteFromScope());
        }
    }
}
