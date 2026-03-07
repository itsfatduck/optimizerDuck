using optimizerDuck.Core.Models.UI;

namespace optimizerDuck.Core.Interfaces;

public interface IToggleFeature
{
    string Name { get; }
    string Description { get; }
    OptimizationRisk Risk { get; }
    Guid Id { get; }
    ToggleFeatureType Type { get; }

    Task<bool> GetStateAsync();
    Task EnableAsync();
    Task DisableAsync();
}
