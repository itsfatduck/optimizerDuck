using System.Collections.Generic;

namespace optimizerDuck.Core.ToggleFeatures;

public abstract class SingleRegistryToggleFeature : RegistryToggleFeature
{
    public abstract RegistryToggle Toggle { get; }

    public override IEnumerable<RegistryToggle> GetToggles()
    {
        yield return Toggle;
    }
}
