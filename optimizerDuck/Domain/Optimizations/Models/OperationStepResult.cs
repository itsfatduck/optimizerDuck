using optimizerDuck.Domain.Abstractions;

namespace optimizerDuck.Domain.Optimizations.Models;

/// <summary>
///     Represents the result of a single operation step within an optimization.
/// </summary>
public record OperationStepResult
{
    /// <summary>
    ///     The one-based index of this step in the execution order.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    ///     The name of the step.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     A description of what this step does.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    ///     Indicates whether the step completed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    ///     The error message if the step failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    ///     An optional action to retry this step.
    /// </summary>
    public Func<Task<bool>>? RetryAction { get; init; }

    /// <summary>
    ///     The revert data generated when applying this step, used to support retry synchronization.
    /// </summary>
    public IRevertStep? RevertStep { get; init; }
}
