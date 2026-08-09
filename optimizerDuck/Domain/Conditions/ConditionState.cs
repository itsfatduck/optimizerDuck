namespace optimizerDuck.Domain.Conditions;

/// <summary>
///     Represents the outcome of a compatibility condition check.
/// </summary>
public enum ConditionState
{
    /// <summary>The item is supported and safe to use on the current system.</summary>
    Available,

    /// <summary>The item is not supported on the current system.</summary>
    Unsupported,

    /// <summary>The condition could not be evaluated.</summary>
    Error,
}
