namespace optimizerDuck.Domain.Exceptions;

/// <summary>
///     Represents an exception that occurred during the execution of an optimization step.
///     Carries both a user-facing error message and optional detailed diagnostic information.
/// </summary>
public class StepExecutionException : Exception
{
    /// <summary>
    ///     Gets optional detailed error information (for example, exception details or stack trace)
    ///     that can aid in diagnosing the failure.
    /// </summary>
    public string? ErrorDetail { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="StepExecutionException"/> class.
    /// </summary>
    /// <param name="error">A user-facing error message describing the failure.</param>
    /// <param name="errorDetail">Optional detailed diagnostic information.</param>
    public StepExecutionException(string? error, string? errorDetail)
        : base(error ?? "Step execution failed")
    {
        ErrorDetail = errorDetail;
    }
}
