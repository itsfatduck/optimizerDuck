using System.Reflection;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Services.Managers;

namespace optimizerDuck.Core.ToggleFeatures;

public abstract class BaseToggleFeature : IToggleFeature
{
    private ToggleFeatureAttribute? _meta;

    private ToggleFeatureAttribute Meta =>
        _meta ??= GetType().GetCustomAttribute<ToggleFeatureAttribute>()
                  ?? throw new InvalidOperationException(
                      $"{GetType().Name} is missing [ToggleFeature] attribute");

    public Type? OwnerType { get; set; }

    public string OwnerKey =>
        OwnerType?.Name
        ?? throw new InvalidOperationException(
            $"{GetType().Name} has no owner assigned");

    public string FeatureKey => GetType().Name;

    public string Name => Loc.Instance[$"ToggleFeature.{FeatureKey}.Name"];
    public string Description => Loc.Instance[$"ToggleFeature.{FeatureKey}.Description"];

    public virtual IEnumerable<RegistryToggle>? RegistryToggles => null;

    public virtual Task<bool> GetStateAsync()
    {
        var toggles = RegistryToggles?.ToList() ?? GetToggles()?.ToList();
        if (toggles == null || toggles.Count == 0)
            return Task.FromResult(false);

        bool allOn = toggles.All(t => t.GetState());
        return Task.FromResult(allOn);
    }

    public virtual Task EnableAsync()
    {
        var toggles = RegistryToggles?.ToList() ?? GetToggles()?.ToList();
        if (toggles == null) return Task.CompletedTask;

        foreach (var toggle in toggles)
            toggle.SetState(true);
        
        return Task.CompletedTask;
    }

    public virtual Task DisableAsync()
    {
        var toggles = RegistryToggles?.ToList() ?? GetToggles()?.ToList();
        if (toggles == null) return Task.CompletedTask;

        foreach (var toggle in toggles)
            toggle.SetState(false);
        
        return Task.CompletedTask;
    }

    protected virtual IEnumerable<RegistryToggle>? GetToggles() => null;
}
