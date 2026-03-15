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
    Failed
}