namespace optimizerDuck.Domain.UI;

/// <summary>
///     Represents the progress of a long-running operation (e.g., optimization, cleanup).
/// </summary>
public record ProcessingProgress
{
    /// <summary>
    ///     A message describing the current operation state.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    ///     Indicates whether the progress is indeterminate (unknown total).
    /// </summary>
    public bool IsIndeterminate { get; init; } = true;

    /// <summary>
    ///     The current progress value.
    /// </summary>
    public int Value { get; init; }

    /// <summary>
    ///     The total number of steps or items.
    /// </summary>
    public int Total { get; init; }
}
