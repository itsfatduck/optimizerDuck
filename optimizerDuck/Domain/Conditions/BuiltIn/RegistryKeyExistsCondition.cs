using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Domain.Conditions;

/// <summary>
///     Base condition that requires a specific registry key to exist on the machine.
///     Subclasses supply the registry item to look up. Failures (e.g. inaccessible hives)
///     propagate to <see cref="ConditionEvaluator"/>, which converts them to an
///     <see cref="ConditionState.Error"/> result.
/// </summary>
public abstract class RegistryKeyExistsCondition : ConditionBase
{
    protected abstract RegistryItem RegistryItem { get; }

    /// <summary>Supplies the localized failure title.</summary>
    protected abstract Func<string> Title { get; }

    /// <summary>Supplies the localized failure description.</summary>
    protected abstract Func<string> Description { get; }

    public override ConditionResult Evaluate(SystemInfo snapshot)
    {
        if (!RegistryService.TryKeyExists(RegistryItem, out var exists))
            return ConditionResult.Error();

        return exists ? ConditionResult.Available : ConditionResult.Unsupported(Title, Description);
    }
}
