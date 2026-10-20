using optimizerDuck.Domain.Conditions;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.UI;
using OptimizationState = optimizerDuck.Domain.UI.OptimizationState;

namespace optimizerDuck.Domain.Abstractions;

/// <summary>
///     Defines a single optimization that can be applied to the system.
/// </summary>
public interface IOptimization
{
    /// <summary>Gets the unique identifier for this optimization.</summary>
    Guid Id { get; }

    /// <summary>Gets the risk level associated with applying this optimization.</summary>
    OptimizationRisk Risk { get; }

    /// <summary>Gets the resource key used for localization lookup.</summary>
    string OptimizationKey { get; }

    /// <summary>Gets the localized display name of the optimization.</summary>
    string Name { get; }

    /// <summary>Gets a localized short description of what this optimization does.</summary>
    string ShortDescription { get; }

    /// <summary>Gets or sets the execution and applied state of the optimization.</summary>
    OptimizationState State { get; set; }

    /// <summary>Gets the condition type implementing <see cref="ICondition" /> that must hold on the system, or <see langword="null" /> if always available.</summary>
    Type? ConditionType { get; }

    /// <summary>Gets or sets the cached result of evaluating <see cref="ConditionType" />.</summary>
    ConditionResult ConditionResult { get; set; }

    /// <summary>
    ///     Applies the optimization to the system.
    /// </summary>
    /// <param name="progress">A progress reporter for UI updates.</param>
    /// <param name="context">The execution context providing logger, system snapshot, and services.</param>
    /// <returns>The result of the apply operation.</returns>
    Task<ApplyResult> ApplyAsync(
        IProgress<ProcessingProgress> progress,
        OptimizationContext context
    );
}
