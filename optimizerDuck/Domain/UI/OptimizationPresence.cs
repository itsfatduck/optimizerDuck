namespace optimizerDuck.Domain.UI;

/// <summary>
///     Real-world state of an optimization derived from both persisted revert data and live
///     inspection of Windows.
/// </summary>
public enum OptimizationPresence
{
    /// <summary>Windows does not match the target and there is no revert record on disk.</summary>
    NotApplied,

    /// <summary>Windows matches the target and revert data exists on disk.</summary>
    Applied,

    /// <summary>Windows already matches the target without revert data from optimizerDuck.</summary>
    AlreadyOptimal,

    /// <summary>Revert data exists on disk, but Windows no longer matches the applied target.</summary>
    Drifted,
}
