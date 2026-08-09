using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Conditions;

/// <summary>Requires Windows 10 (build 10240 to 21999).</summary>
public sealed class Windows10Condition : ConditionBase
{
    public override ConditionResult Evaluate(SystemSnapshot snapshot) =>
        TryGetOsBuild(snapshot, out var build) && build is >= WindowsBuilds.Windows10 and < WindowsBuilds.Windows11
            ? ConditionResult.Available
            : ConditionResult.Unsupported(
                () => Translations.Condition_Windows10_Title,
                () => Translations.Condition_Windows10_Description
            );
}
