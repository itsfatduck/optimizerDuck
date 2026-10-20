namespace optimizerDuck.Domain.Customize.Models;

/// <summary>
///     Represents the recommendation state and explanation for a customization setting.
/// </summary>
/// <param name="State">The recommendation state for this setting.</param>
/// <param name="ReasonTranslationKey">The localization key describing the rationale for the recommendation.</param>
public sealed record CustomizeRecommendationResult(
    RecommendationState State,
    string ReasonTranslationKey
);
