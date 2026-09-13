using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Conditions;

/// <summary>
///     Base class for conditions. Provides helpers shared by condition implementations.
/// </summary>
public abstract class ConditionBase : ICondition
{
    /// <inheritdoc />
    public abstract ConditionResult Evaluate(SystemInfo snapshot);

    /// <summary>Reads the major OS build number (e.g. 22631) from the snapshot.</summary>
    protected static bool TryGetOsBuild(SystemInfo snapshot, out int build)
    {
        build = snapshot.Windows.BuildNumber ?? 0;
        return snapshot.Windows.BuildNumber.HasValue;
    }
}
