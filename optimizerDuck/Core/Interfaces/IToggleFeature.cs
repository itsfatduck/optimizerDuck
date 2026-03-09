using optimizerDuck.Core.Models.UI;

namespace optimizerDuck.Core.Interfaces;

public interface IToggleFeature
{
    string Name { get; }
    string Description { get; }
    string Section { get; }
    string FeatureKey { get; }

    Task<bool> GetStateAsync();
    Task EnableAsync();
    Task DisableAsync();
}
