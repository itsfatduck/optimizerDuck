using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Conditions;

/// <summary>
///     Provides helpers shared by condition implementations.
/// </summary>
public abstract class ConditionBase : ICondition
{
    /// <inheritdoc />
    public abstract ConditionResult Evaluate(SystemInfo snapshot);

    /// <summary>
    ///     Reads the major OS build number from the system snapshot.
    /// </summary>
    /// <param name="snapshot">The system snapshot to read the build number from.</param>
    /// <param name="build">When this method returns, contains the OS build number if present; otherwise, 0.</param>
    /// <returns><see langword="true" /> if the build number was present; otherwise, <see langword="false" />.</returns>
    protected static bool TryGetOsBuild(SystemInfo snapshot, out int build)
    {
        build = snapshot.Windows.BuildNumber ?? 0;
        return snapshot.Windows.BuildNumber.HasValue;
    }
}
