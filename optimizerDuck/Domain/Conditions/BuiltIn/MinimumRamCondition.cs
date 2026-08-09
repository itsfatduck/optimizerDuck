using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Conditions;

/// <summary>Base condition that requires a minimum amount of installed RAM.</summary>
public abstract class MinimumRamCondition : ConditionBase
{
    /// <summary>The minimum required RAM in gigabytes.</summary>
    protected abstract double MinimumGb { get; }

    /// <summary>Strongly-typed localized failure title.</summary>
    protected abstract Func<string> Title { get; }

    /// <summary>Strongly-typed localized failure description.</summary>
    protected abstract Func<string> Description { get; }

    public override ConditionResult Evaluate(SystemSnapshot snapshot) =>
        snapshot.Ram.TotalGB >= MinimumGb
            ? ConditionResult.Available
            : ConditionResult.Unsupported(Title, Description);
}

/// <summary>Requires at least 16 GB of installed RAM.</summary>
public sealed class SixteenGbRamCondition : MinimumRamCondition
{
    protected override double MinimumGb => 16;
    protected override Func<string> Title => () => Translations.Condition_Ram_16_Title;
    protected override Func<string> Description => () => Translations.Condition_Ram_16_Description;
}
