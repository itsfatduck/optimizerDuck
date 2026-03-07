using System.Reflection;
using optimizerDuck.Core.Interfaces;

namespace optimizerDuck.Core.ToggleFeatures;

public abstract class RegistryToggleFeature : BaseToggleFeature
{
    private RegistryToggle? _toggle;

    protected RegistryToggle GetToggle()
    {
        if (_toggle != null)
            return _toggle;

        var toggleProperty = GetType().GetProperty("Toggle", BindingFlags.Public | BindingFlags.Instance);
        if (toggleProperty?.PropertyType == typeof(RegistryToggle))
        {
            _toggle = (RegistryToggle?)toggleProperty.GetValue(this);
        }

        return _toggle!;
    }

    public override Task<bool> GetStateAsync()
    {
        var toggle = GetToggle();
        if (toggle == null)
            return Task.FromResult(false);
        return Task.FromResult(toggle.GetState());
    }

    public override Task EnableAsync()
    {
        var toggle = GetToggle();
        toggle?.SetState(true);
        return Task.CompletedTask;
    }

    public override Task DisableAsync()
    {
        var toggle = GetToggle();
        toggle?.SetState(false);
        return Task.CompletedTask;
    }
}
