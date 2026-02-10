using optimizerDuck.Core.Models.Optimization;
using optimizerDuck.Core.Models.UI;
using OptimizationState = optimizerDuck.Core.Models.UI.OptimizationState;

namespace optimizerDuck.Core.Interfaces;

public interface IOptimization
{
    Guid Id { get; }
    OptimizationRisk Risk { get; }
    
    string OptimizationKey { get; }

    string Name { get; }
    string ShortDescription { get; }
    OptimizationState State { get; set; }

    Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context);
}