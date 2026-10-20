using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Conditions;

/// <summary>
///     Evaluates whether a single compatibility requirement is met on the current system.
///     Implementations must have a parameterless constructor so they can be instantiated
///     from the <c>Condition = typeof(...)</c> attribute metadata via reflection.
/// </summary>
public interface ICondition
{
    /// <summary>
    ///     Evaluates whether the condition is met on the current system.
    /// </summary>
    /// <param name="snapshot">The system hardware and OS snapshot to evaluate against.</param>
    /// <returns>A <see cref="ConditionResult" /> representing the outcome of the evaluation.</returns>
    ConditionResult Evaluate(SystemInfo snapshot);
}
