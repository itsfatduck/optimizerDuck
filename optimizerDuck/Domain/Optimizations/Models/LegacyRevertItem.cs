using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Conditions;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Domain.Optimizations.Models;

/// <summary>
///     Represents revert data left on disk from an older optimization class that has since been
///     retired from the active catalog.
/// </summary>
public sealed class LegacyRevertItem(
    Guid id,
    string optimizationKey,
    string displayName,
    DateTime appliedAt,
    int stepCount
) : LocalizedObject
{
    public Guid Id { get; } = id;

    public string OptimizationKey { get; } = optimizationKey;

    public string DisplayName { get; } = displayName;

    public DateTime AppliedAt { get; } = appliedAt;

    public int StepCount { get; } = stepCount;

    /// <summary>Gets a localized summary of how many recorded steps will be undone.</summary>
    public string StepsSummary => Loc.Instance["Optimizer.Legacy.StepCount", StepCount];

    /// <summary>Gets a localized timestamp summary of when the legacy optimization was applied.</summary>
    public string AppliedAtSummary =>
        Loc.Instance["Optimizer.Legacy.AppliedAt", AppliedAt.ToString("g", Loc.CurrentCulture)];

    /// <summary>Builds a minimal <see cref="IOptimization"/> adapter for <c>RevertManager</c>.</summary>
    public IOptimization AsOptimization() => new LegacyRevertAdapter(this);

    private sealed class LegacyRevertAdapter(LegacyRevertItem item) : IOptimization
    {
        public Guid Id => item.Id;
        public OptimizationRisk Risk => OptimizationRisk.Moderate;
        public string OptimizationKey => item.OptimizationKey;
        public string Name => item.DisplayName;
        public string ShortDescription => string.Empty;
        public OptimizationState State { get; set; } =
            new() { IsApplied = true, HasRevertData = true };
        public Type? ConditionType => null;
        public ConditionResult ConditionResult { get; set; } = ConditionResult.Available;

        public Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        ) => throw new NotSupportedException("Legacy optimizations can only be reverted.");
    }
}
