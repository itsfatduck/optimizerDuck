using optimizerDuck.Domain.Optimizations.Models;

namespace optimizerDuck.Domain.Revert;

/// <summary>
///     Represents the result of a revert operation, including success status,
///     a human-readable message, and details about any steps that failed.
/// </summary>
public class RevertResult
{
    /// <summary>
    ///     Gets or sets a value that indicates whether the revert was fully successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     Gets or sets a human-readable message describing the result.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the exception that occurred, if any, during the revert.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    ///     Gets or sets the list of steps that failed during the revert operation.
    /// </summary>
    public List<OperationStepResult> FailedSteps { get; set; } = [];

    /// <summary>
    ///     Gets or sets a value that indicates whether the revert failed completely
    ///     (that is, all individual steps failed).
    /// </summary>
    public bool AllStepsFailed { get; set; }
}
