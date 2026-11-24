using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Conditions;

/// <summary>Requires Memory Integrity (HVCI) capability on Windows 11.</summary>
public sealed class MemoryIntegritySupportedCondition : ConditionBase
{
    public override ConditionResult Evaluate(SystemInfo snapshot)
    {
        if (snapshot.Security.MemoryIntegrity == VbsState.Unknown)
            return ConditionResult.Error();

        return snapshot.Security.MemoryIntegrity != VbsState.Unavailable
            ? ConditionResult.Available
            : ConditionResult.Unsupported(
                () => Loc.Instance["Condition.MemoryIntegrity.Title"],
                () => Loc.Instance["Condition.MemoryIntegrity.Description"]
            );
    }
}
