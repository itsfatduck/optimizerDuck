namespace optimizerDuck.Domain.UI;

/// <summary>
///     Indicates the result status of an optimization apply or revert operation.
/// </summary>
public enum OptimizationSuccessResult
{
    Success,

    /// <summary>Some steps succeeded while others failed.</summary>
    PartialSuccess,

    Failed,

    /// <summary>
    ///     Nothing needed to change because the machine already matched the optimization.
    ///     No revert data exists for it.
    /// </summary>
    NothingToDo,
}
