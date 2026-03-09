using System.Reflection;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Services.Managers;

namespace optimizerDuck.Core.Models.ToggleFeatures;

public abstract class BaseToggleFeature : IToggleFeature
{
    private ToggleFeatureAttribute? _meta;

    private ToggleFeatureAttribute Meta =>
        _meta ??= GetType().GetCustomAttribute<ToggleFeatureAttribute>()
                  ?? throw new InvalidOperationException(
                      $"{GetType().Name} is missing [ToggleFeature] attribute");

    public Type? OwnerType { get; set; }

    public string FeatureKey => GetType().Name;

    public string Name => Loc.Instance[$"ToggleFeature.{FeatureKey}.Name"];
    public string Description => Loc.Instance[$"ToggleFeature.{FeatureKey}.Description"];
    public string Section
    {
        get
        {
            var section = Meta.GetSectionName();
            return string.IsNullOrEmpty(section) ? string.Empty : Loc.Instance[$"ToggleFeature.Category.{OwnerType?.Name}.Section.{section}"];
        }
    }

    protected virtual IEnumerable<RegistryToggle>? RegistryToggles => null;

    public virtual Task<bool> GetStateAsync()
    {
        var toggles = RegistryToggles;
        if (toggles == null)
            return Task.FromResult(false);

        return Task.FromResult(toggles.All(t => t.GetState()));
    }

    public virtual Task EnableAsync() => SetTogglesState(true);

    public virtual Task DisableAsync() => SetTogglesState(false);

    private Task SetTogglesState(bool isOn)
    {
        var toggles = RegistryToggles;
        if (toggles != null)
        {
            foreach (var toggle in toggles)
                toggle.SetState(isOn);
        }
        return Task.CompletedTask;
    }
}
