using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Conditions;

/// <summary>Requires Windows 11 (build 22000 or later).</summary>
public sealed class Windows11Condition : ConditionBase
{
    public override ConditionResult Evaluate(SystemSnapshot snapshot) =>
        TryGetOsBuild(snapshot, out var build) && build >= WindowsBuilds.Windows11
            ? ConditionResult.Available
            : ConditionResult.Unsupported(
                () => Loc.Instance["Condition.Windows11.Title"],
                () => Loc.Instance["Condition.Windows11.Description"]
            );
}
