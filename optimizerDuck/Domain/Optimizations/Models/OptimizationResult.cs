using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.UI;

namespace optimizerDuck.Domain.Optimizations.Models;

public record OptimizationResult
{
    /// <summary>Overall outcome: Success, PartialSuccess, or Failed.</summary>
    public OptimizationSuccessResult Status { get; init; }

    public string Message { get; init; } = string.Empty;

    public Exception? Exception { get; init; }

    public List<Change> FailedSteps { get; init; } = [];
}
