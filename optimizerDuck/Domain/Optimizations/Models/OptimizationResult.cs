using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.UI;

namespace optimizerDuck.Domain.Optimizations.Models;

/// <summary>
///     Represents the outcome of executing an optimization.
/// </summary>
public record OptimizationResult
{
    /// <summary>Gets the overall outcome status of the optimization.</summary>
    public OptimizationSuccessResult Status { get; init; }

    /// <summary>Gets the user-facing message describing the result.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Gets the exception that caused the optimization to fail, if any.</summary>
    public Exception? Exception { get; init; }

    /// <summary>Gets the list of individual steps that failed during execution.</summary>
    public List<Change> FailedSteps { get; init; } = [];
}
