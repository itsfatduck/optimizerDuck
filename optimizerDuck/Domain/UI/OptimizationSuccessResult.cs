namespace optimizerDuck.Domain.UI;

/// <summary>
///     Indicates the result status of an optimization apply or revert operation.
/// </summary>
public enum OptimizationSuccessResult
{
    /// <summary>All steps completed successfully.</summary>
    Success,

    /// <summary>Some steps succeeded while others failed.</summary>
    PartialSuccess,

    /// <summary>The operation failed entirely.</summary>
    Failed,

    /// <summary>
    ///     Nothing needed to change because the machine already matched the optimization.
    ///     No revert data exists for it.
    /// </summary>
    NothingToDo,
}
