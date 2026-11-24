using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Conditions;

/// <summary>Base condition for optimizations targeted at systems up to a RAM ceiling.</summary>
public abstract class MaximumRamCondition : ConditionBase
{
    protected abstract double MaximumGb { get; }
    protected abstract Func<string> Title { get; }
    protected abstract Func<string> Description { get; }

    public override ConditionResult Evaluate(SystemInfo snapshot)
    {
        if (snapshot.Memory.TotalBytes <= 0)
            return ConditionResult.Error();

        return snapshot.Memory.TotalGB <= MaximumGb
            ? ConditionResult.Available
            : ConditionResult.Unsupported(Title, Description);
    }
}

/// <summary>Targets systems with 12 GB or less of installed RAM.</summary>
public sealed class TwelveGbMaxRamCondition : MaximumRamCondition
{
    protected override double MaximumGb => 12.5;
    protected override Func<string> Title => () => Loc.Instance["Condition.MaxRam.12.Title"];
    protected override Func<string> Description =>
        () => Loc.Instance["Condition.MaxRam.12.Description"];
}
