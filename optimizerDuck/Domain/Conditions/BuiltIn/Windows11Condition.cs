using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Conditions;

/// <summary>Requires Windows 11 (build 22000 or later).</summary>
public sealed class Windows11Condition : ConditionBase
{
    public override ConditionResult Evaluate(SystemSnapshot snapshot) =>
        TryGetOsBuild(snapshot, out var build) && build >= WindowsBuilds.Windows11
            ? ConditionResult.Available
            : ConditionResult.Unsupported(
                () => Translations.Condition_Windows11_Title,
                () => Translations.Condition_Windows11_Description
            );
}
