using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Conditions;

/// <summary>
///     Base class for conditions. Provides helpers shared by condition implementations.
/// </summary>
public abstract class ConditionBase : ICondition
{
    /// <inheritdoc />
    public abstract ConditionResult Evaluate(SystemSnapshot snapshot);

    /// <summary>Attempts to parse the OS build number (e.g. "22631") from the snapshot.</summary>
    protected static bool TryGetOsBuild(SystemSnapshot snapshot, out int build)
    {
        build = 0;
        if (snapshot.Os.BuildNumber is not { } text)
            return false;

        var span = text.AsSpan();
        var dot = span.IndexOf('.');
        if (dot >= 0)
            span = span[..dot];
        return int.TryParse(span, out build);
    }
}
