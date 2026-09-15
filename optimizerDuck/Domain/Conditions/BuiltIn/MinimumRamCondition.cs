using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Conditions;

/// <summary>Base condition that requires a minimum amount of installed RAM.</summary>
public abstract class MinimumRamCondition : ConditionBase
{
    protected abstract double MinimumGb { get; }

    /// <summary>Supplies the localized failure title.</summary>
    protected abstract Func<string> Title { get; }

    /// <summary>Supplies the localized failure description.</summary>
    protected abstract Func<string> Description { get; }

    public override ConditionResult Evaluate(SystemInfo snapshot) =>
        snapshot.Memory.TotalGB >= MinimumGb
            ? ConditionResult.Available
            : ConditionResult.Unsupported(Title, Description);
}

/// <summary>Requires at least 16 GB of installed RAM.</summary>
public sealed class SixteenGbRamCondition : MinimumRamCondition
{
    protected override double MinimumGb => 16;
    protected override Func<string> Title => () => Loc.Instance["Condition.Ram.16.Title"];
    protected override Func<string> Description =>
        () => Loc.Instance["Condition.Ram.16.Description"];
}
