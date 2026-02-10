using optimizerDuck.Core.Models.UI;

namespace optimizerDuck.Core.Models.Optimization;

public record OptimizationResult
{
    public OptimizationSuccessResult Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public Exception? Exception { get; init; }
    public List<OperationStepResult> FailedSteps { get; init; } = [];
}