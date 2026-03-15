using optimizerDuck.Domain.Optimizations.Models;

namespace optimizerDuck.Domain.Revert;

/// <summary>
///     Represents the result of a revert operation.
/// </summary>
public class RevertResult
{
    /// <summary>
    ///     Indicates whether the revert was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     A message describing the result.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    ///     The exception that occurred, if any.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    ///     Details of any steps that failed during revert.
    /// </summary>
    public List<OperationStepResult> FailedStepDetails { get; set; } = [];

    /// <summary>
    ///     Indicates whether the revert failed completely (all steps failed).
    /// </summary>
    public bool IsCompleteFailure { get; set; }
}