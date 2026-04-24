using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.UI;
using OptimizationState = optimizerDuck.Domain.UI.OptimizationState;

namespace optimizerDuck.Domain.Abstractions;

/// <summary>
///     Defines a single optimization that can be applied to the system.
/// </summary>
public interface IOptimization
{
    /// <summary>
    ///     The unique identifier for this optimization.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    ///     The risk level associated with this optimization.
    /// </summary>
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

    /// <summary>
    ///     The current applied state and timing information.
    /// </summary>
    OptimizationState State { get; set; }

    /// <summary>
    ///     Applies this optimization to the system.
    /// </summary>
    /// <param name="progress">A progress reporter for UI updates.</param>
    /// <param name="context">The context providing logger, system snapshot, and services.</param>
    /// <returns>A task that completes with the result of the apply operation.</returns>
    Task<ApplyResult> ApplyAsync(
        IProgress<ProcessingProgress> progress,
        OptimizationContext context
    );
}
