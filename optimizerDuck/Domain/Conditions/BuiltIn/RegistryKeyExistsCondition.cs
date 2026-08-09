using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Services.Optimization.Providers;
using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Conditions;

/// <summary>
///     Base condition that requires a specific registry key to exist on the machine.
///     Subclasses supply the registry item to look up. Failures (e.g. inaccessible hives)
///     propagate to <see cref="ConditionEvaluator"/>, which converts them to an
///     <see cref="ConditionState.Error"/> result.
/// </summary>
public abstract class RegistryKeyExistsCondition : ConditionBase
{
    /// <summary>The registry key that must exist.</summary>
    protected abstract RegistryItem RegistryItem { get; }

    /// <summary>Strongly-typed localized failure title.</summary>
    protected abstract Func<string> Title { get; }

    /// <summary>Strongly-typed localized failure description.</summary>
    protected abstract Func<string> Description { get; }

    public override ConditionResult Evaluate(SystemSnapshot snapshot) =>
        RegistryService.KeyExists(RegistryItem)
            ? ConditionResult.Available
            : ConditionResult.Unsupported(Title, Description);
}
