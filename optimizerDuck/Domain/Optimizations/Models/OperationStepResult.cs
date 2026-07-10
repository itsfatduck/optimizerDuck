using optimizerDuck.Domain.Abstractions;

namespace optimizerDuck.Domain.Optimizations.Models;

/// <summary>
///     Represents the result of a single operation step within an optimization.
/// </summary>
public record OperationStepResult
{
    /// <summary>
    ///     Gets the one-based index of this step in the execution order.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    ///     Gets the name of the step.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     Gets a description of what this step does.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    ///     Gets a value that indicates whether the step completed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    ///     Gets the error message if the step failed, or <see langword="null"/> if it succeeded.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    ///     Gets detailed error information (exception details) for the step failure.
    /// </summary>
    public string? ErrorDetail { get; init; }

    /// <summary>
    ///     Gets an optional action to retry this step.
    /// </summary>
    public Func<Task<bool>>? RetryAction { get; init; }

    /// <summary>
    ///     Gets the revert data generated when applying this step, used to support retry synchronization.
    /// </summary>
    public IRevertStep? RevertStep { get; init; }
}
