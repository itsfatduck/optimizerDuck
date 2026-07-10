namespace optimizerDuck.Domain.Customize.Models;

/// <summary>
///     Specifies the recommended state for a customize setting.
/// </summary>
public enum RecommendationState
{
    /// <summary>No recommendation is provided.</summary>
    None,

    /// <summary>The setting is recommended to be turned on.</summary>
    On,

    /// <summary>The setting is recommended to be turned off.</summary>
    Off,

    /// <summary>The setting is experimental and may cause unexpected behavior.</summary>
    Experimental,

    /// <summary>The recommendation depends on other factors (e.g., hardware or user preference).</summary>
    Depends,
}
