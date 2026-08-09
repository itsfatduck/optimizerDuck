using System.IO;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Conditions;

/// <summary>
///     Requires Windows Recall (AI Explorer) to actually be present on the machine.
///     Recall ships with the Windows AI shell components, which are only installed on
///     Windows 11 24H2+ systems that have the feature: a build check alone would mark
///     a plain 24H2 machine as supported even though Recall does not exist there.
/// </summary>
public sealed class RecallInstalledCondition : ConditionBase
{
    // Recall's AI shell components (speech/AI runtime) live here on machines where the
    // feature is installed; the directory is absent on systems without it.
    private const string AiShellDirectory = @"C:\Windows\System32\CoreAISpeech";

    public override ConditionResult Evaluate(SystemSnapshot snapshot)
    {
        var isSupported =
            TryGetOsBuild(snapshot, out var build)
            && build >= WindowsBuilds.Windows11_24H2
            && Directory.Exists(AiShellDirectory);

        return isSupported
            ? ConditionResult.Available
            : ConditionResult.Unsupported(
                () => Translations.Condition_Recall_Title,
                () => Translations.Condition_Recall_Description
            );
    }
}
