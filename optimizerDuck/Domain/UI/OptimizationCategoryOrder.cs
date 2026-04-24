namespace optimizerDuck.Domain.UI;

/// <summary>
///     Specifies the display order of optimization categories in the UI.
/// </summary>
public enum OptimizationCategoryOrder
{
    /// <summary>Performance-related optimizations.</summary>
    Performance,

    /// <summary>Security and privacy optimizations.</summary>
    SecurityAndPrivacy,

    /// <summary>GPU-specific optimizations.</summary>
    Gpu,

    /// <summary>Power management optimizations.</summary>
    Power,

    /// <summary>Bloatware removal and service management.</summary>
    BloatwareAndServices,

    /// <summary>User experience and visual optimizations.</summary>
    UserExperience,
}
