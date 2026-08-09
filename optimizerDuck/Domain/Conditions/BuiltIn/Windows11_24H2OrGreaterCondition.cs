using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Conditions;

/// <summary>
///     Requires Windows 11 24H2 (build 26100) or later. Used to gate AI features
///     that only exist on 24H2 and newer builds.
/// </summary>
public sealed class Windows11_24H2OrGreaterCondition : ConditionBase
{
    public override ConditionResult Evaluate(SystemSnapshot snapshot) =>
        TryGetOsBuild(snapshot, out var build) && build >= WindowsBuilds.Windows11_24H2
            ? ConditionResult.Available
            : ConditionResult.Unsupported(
                () => Translations.Condition_Windows11_24H2_Title,
                () => Translations.Condition_Windows11_24H2_Description
            );
}
