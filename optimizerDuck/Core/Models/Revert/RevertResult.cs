using optimizerDuck.Core.Models.Optimization;

namespace optimizerDuck.Core.Models.Revert;

public class RevertResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }

    public List<OperationStepResult> FailedStepDetails { get; set; } = [];
}