namespace optimizerDuck.Domain.Features.Models;

public sealed record FeatureRecommendationResult(
    RecommendationState State,
    string ReasonTranslationKey
);
