using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Conditions;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.System;
using OptimizationState = optimizerDuck.Domain.UI.OptimizationState;

namespace optimizerDuck.Test.TestDoubles;

public abstract class StubOptimization : IOptimization
{
    public virtual Guid Id { get; init; } = Guid.NewGuid();
    public virtual OptimizationRisk Risk { get; init; } = OptimizationRisk.Safe;
    public virtual string OptimizationKey { get; init; } = "TestOptimization";
    public virtual string Name { get; init; } = "Test Optimization";
    public virtual string ShortDescription { get; init; } = "Test optimization";
    public virtual OptimizationMetadata Metadata { get; init; } = OptimizationMetadata.Empty;
    public virtual OptimizationState State { get; set; } = new();
    public virtual Type? ConditionType { get; init; }
    public virtual ConditionResult ConditionResult { get; set; } = ConditionResult.Available;

    public virtual Task<bool?> IsSystemAtTargetAsync(
        SystemInfo snapshot,
        IReadOnlyDictionary<string, string>? appliedOptions = null,
        CancellationToken cancellationToken = default
    ) => Task.FromResult<bool?>(null);

    public abstract Task<ApplyResult> ApplyAsync(
        IProgress<ProcessingProgress> progress,
        OptimizationContext context
    );
}
