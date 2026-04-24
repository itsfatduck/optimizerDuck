namespace optimizerDuck.Domain.UI;

/// <summary>
///     Specifies the risk level associated with an optimization.
/// </summary>
public enum OptimizationRisk
{
    /// <summary>Safe to apply with no adverse effects.</summary>
    Safe,

    /// <summary>May cause minor side effects; recommended for experienced users.</summary>
    Moderate,

    /// <summary>High risk; may cause system instability or data loss. Use with caution.</summary>
    Risky,
}
