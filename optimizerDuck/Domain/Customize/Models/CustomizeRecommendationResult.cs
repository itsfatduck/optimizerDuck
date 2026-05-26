namespace optimizerDuck.Domain.Customize.Models;

public sealed record CustomizeRecommendationResult(
    RecommendationState State,
    string ReasonTranslationKey
);
