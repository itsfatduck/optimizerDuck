namespace optimizerDuck.Domain.UI;

/// <summary>
///     Progress of a long-running operation.
/// </summary>
public record ProcessingProgress
{
    public string Message { get; init; } = string.Empty;

    /// <summary>Indeterminate progress has no known total.</summary>
    public bool IsIndeterminate { get; init; } = false;

    public int Value { get; init; }

    public int Total { get; init; }
}
