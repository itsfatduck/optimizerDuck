using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Conditions;

/// <summary>
///     Evaluates whether a single compatibility requirement is met on the current system.
///     Implementations must have a parameterless constructor so they can be instantiated
///     from the <c>Condition = typeof(...)</c> attribute metadata via reflection.
/// </summary>
public interface ICondition
{
    /// <summary>Evaluates the condition against the system snapshot.</summary>
    ConditionResult Evaluate(SystemSnapshot snapshot);
}
