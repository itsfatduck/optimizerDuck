using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using optimizerDuck.Domain.Conditions;
using optimizerDuck.Services.System;

namespace optimizerDuck.Services.Conditions;

/// <summary>
///     Evaluates the compatibility conditions declared on optimizations and customize
///     settings against a system snapshot. Fails open when the snapshot is unpopulated,
///     so incomplete hardware detection never hides an item.
/// </summary>
public static class ConditionEvaluator
{
    private static readonly ConcurrentDictionary<Type, ICondition> ConditionCache = new();

    private static ICondition GetOrCreateCondition(Type conditionType) =>
        ConditionCache.GetOrAdd(
            conditionType,
            static t => (ICondition)Activator.CreateInstance(t)!
        );

    /// <summary>
    ///     Evaluates the condition declared by <paramref name="conditionType"/>, or returns
    ///     <see cref="ConditionResult.Available"/> when there is no condition. Never throws.
    /// </summary>
    /// <param name="conditionType">The condition type declared in an attribute, or <c>null</c>.</param>
    /// <param name="snapshot">The current system snapshot to evaluate against.</param>
    /// <param name="logger">Logger for evaluation diagnostics.</param>
    public static ConditionResult Evaluate(
        Type? conditionType,
        SystemSnapshot snapshot,
        ILogger logger
    )
    {
        if (conditionType is null)
            return ConditionResult.Available;

        // Fail open: an unpopulated snapshot means hardware detection has not finished (or
        // failed), so never hide an item based on incomplete data. Callers re-evaluate once
        // a real snapshot is available.
        if (ReferenceEquals(snapshot, SystemSnapshot.Unknown))
            return ConditionResult.Available;

        try
        {
            return GetOrCreateCondition(conditionType).Evaluate(snapshot);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Condition {ConditionType} failed to evaluate", conditionType);
            return ConditionResult.Error();
        }
    }

    /// <summary>
    ///     Evaluates the condition of every item in <paramref name="items"/> and pushes the
    ///     result through <paramref name="apply"/>.
    /// </summary>
    /// <param name="items">The items whose conditions are evaluated.</param>
    /// <param name="conditionTypeOf">Returns the declared condition type for an item.</param>
    /// <param name="apply">Receives each item with its evaluated result.</param>
    /// <param name="snapshot">The system snapshot to evaluate against.</param>
    /// <param name="logger">Logger for evaluation diagnostics.</param>
    public static void EvaluateAll<T>(
        IEnumerable<T> items,
        Func<T, Type?> conditionTypeOf,
        Action<T, ConditionResult> apply,
        SystemSnapshot snapshot,
        ILogger logger
    )
    {
        foreach (var item in items)
            apply(item, Evaluate(conditionTypeOf(item), snapshot, logger));
    }
}
