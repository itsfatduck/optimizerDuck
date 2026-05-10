using optimizerDuck.Domain.Features.Models;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Abstractions;

public interface IFeature
{
    string Name { get; }
    string Description { get; }
    string Section { get; }
    public SymbolRegular Icon { get; }
    string FeatureKey { get; }

    FeatureRecommendationResult? GetRecommendation();

    Task<bool> GetStateAsync();
    Task EnableAsync();
    Task DisableAsync();
}
