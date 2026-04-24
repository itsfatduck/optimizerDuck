namespace optimizerDuck.Domain.Optimizations.Models;

/// <summary>
///     Represents the result of applying a single optimization step.
/// </summary>
public readonly record struct ApplyResult
{
    /// <summary>
    ///     The error message if the step failed; <c>null</c> if successful.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    ///     Creates a successful result.
    /// </summary>
    /// <returns>An <see cref="ApplyResult" /> with no error message.</returns>
    public static ApplyResult True()
    {
        return new ApplyResult { Message = null };
    }

    /// <summary>
    ///     Creates a failed result with an error message.
    /// </summary>
    /// <param name="message">The error message describing the failure.</param>
    /// <returns>An <see cref="ApplyResult" /> containing the error message.</returns>
    public static ApplyResult False(string message)
    {
        return new ApplyResult { Message = message };
    }
}
