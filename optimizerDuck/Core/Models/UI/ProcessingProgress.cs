namespace optimizerDuck.Core.Models.UI;

public record ProcessingProgress
{
    public string Message { get; init; } = string.Empty;
    public bool IsIndeterminate { get; init; } = true;
    public int Value { get; init; }
    public int Total { get; init; }
}