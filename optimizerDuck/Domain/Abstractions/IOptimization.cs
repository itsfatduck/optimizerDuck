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
    Guid Id { get; }

    OptimizationRisk Risk { get; }

    /// <summary>
    ///     The resource key used for localization lookup.
    /// </summary>
    string OptimizationKey { get; }

    /// <summary>
    ///     The localized display name of the optimization.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     A localized short description of what this optimization does.
    /// </summary>
    string ShortDescription { get; }

    OptimizationState State { get; set; }

    /// <summary>
    ///     The <see cref="ICondition" /> implementation that must hold on the current system,
    ///     or <see langword="null" /> when the optimization is always available.
    /// </summary>
    Type? ConditionType { get; }

    ConditionResult ConditionResult { get; set; }

    /// <param name="progress">A progress reporter for UI updates.</param>
    /// <param name="context">The context providing logger, system snapshot, and services.</param>
    /// <returns>The result of the apply operation.</returns>
    Task<ApplyResult> ApplyAsync(
        IProgress<ProcessingProgress> progress,
        OptimizationContext context
    );
}
