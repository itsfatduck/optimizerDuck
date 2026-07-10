using optimizerDuck.Domain.UI;

namespace optimizerDuck.Domain.Optimizations.Models;

/// <summary>
///     Represents the result of an optimization apply operation.
///     Includes the overall status, a human-readable message, and details
///     about any steps that failed.
/// </summary>
public record OptimizationResult
{
    /// <summary>
    ///     Gets the status of the operation (Success, PartialSuccess, or Failed).
    /// </summary>
    public OptimizationSuccessResult Status { get; init; }

    /// <summary>
    ///     Gets a human-readable message describing the result.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the exception that occurred, if any, during the apply operation.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    ///     Gets the list of steps that failed during the operation.
    /// </summary>
    public List<OperationStepResult> FailedSteps { get; init; } = [];
}
