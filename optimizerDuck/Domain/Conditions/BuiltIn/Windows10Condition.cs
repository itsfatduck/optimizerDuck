using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Conditions;

/// <summary>Requires Windows 10 (build 10240 to 21999).</summary>
public sealed class Windows10Condition : ConditionBase
{
    public override ConditionResult Evaluate(SystemSnapshot snapshot) =>
        TryGetOsBuild(snapshot, out var build)
        && build is >= WindowsBuilds.Windows10 and < WindowsBuilds.Windows11
            ? ConditionResult.Available
            : ConditionResult.Unsupported(
                () => Loc.Instance["Condition.Windows10.Title"],
                () => Loc.Instance["Condition.Windows10.Description"]
            );
}
