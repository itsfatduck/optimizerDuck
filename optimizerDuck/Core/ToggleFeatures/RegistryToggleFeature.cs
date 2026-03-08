using System.Reflection;
using optimizerDuck.Core.Interfaces;

namespace optimizerDuck.Core.ToggleFeatures;

public abstract class RegistryToggleFeature : BaseToggleFeature
{
    public abstract IEnumerable<RegistryToggle> GetToggles();

    public override Task<bool> GetStateAsync()
    {
        var toggles = GetToggles().ToList();
        if (toggles.Count == 0)
            return Task.FromResult(false);

        // Feature is considered ON if ALL toggles are ON
        bool allOn = toggles.All(t => t.GetState());
        return Task.FromResult(allOn);
    }

    public override Task EnableAsync()
    {
        foreach (var toggle in GetToggles())
        {
            toggle.SetState(true);
        }
        return Task.CompletedTask;
    }

    public override Task DisableAsync()
    {
        foreach (var toggle in GetToggles())
        {
            toggle.SetState(false);
        }
        return Task.CompletedTask;
    }
}
