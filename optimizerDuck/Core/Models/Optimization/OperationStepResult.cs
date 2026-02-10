namespace optimizerDuck.Core.Models.Optimization;

public record OperationStepResult
{
    public int Index { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? Error { get; init; }
    public Func<Task<bool>>? RetryAction { get; init; }
}