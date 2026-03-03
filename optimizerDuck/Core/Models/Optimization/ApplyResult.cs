namespace optimizerDuck.Core.Models.Optimization;

/// <summary>
///     Represents the result of applying a single optimization step.
/// </summary>
public readonly record struct ApplyResult(string? Message)
{
    /// <summary>
    ///     Creates a successful result.
    /// </summary>
    public static ApplyResult True()
    {
        return new ApplyResult(null);
    }

    /// <summary>
    ///     Creates a failed result with an error message.
    /// </summary>
    public static ApplyResult False(string message)
    {
        return new ApplyResult(message);
    }
}