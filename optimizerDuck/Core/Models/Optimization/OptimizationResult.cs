using optimizerDuck.Core.Models.UI;

namespace optimizerDuck.Core.Models.Optimization;

/// <summary>
///     Represents the result of an optimization apply operation.
/// </summary>
public record OptimizationResult
{
    /// <summary>
    ///     The status of the operation (Success, PartialSuccess, or Failed).
    /// </summary>
    public OptimizationSuccessResult Status { get; init; }
    /// <summary>
    ///     A message describing the result.
    /// </summary>
    public string Message { get; init; } = string.Empty;
    /// <summary>
    ///     The exception that occurred, if any.
    /// </summary>
    public Exception? Exception { get; init; }
    /// <summary>
    ///     List of steps that failed during the operation.
    /// </summary>
    public List<OperationStepResult> FailedSteps { get; init; } = [];
}