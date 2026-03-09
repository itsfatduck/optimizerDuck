using System.Reflection;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Services.Managers;

namespace optimizerDuck.Core.Models.Features;

public abstract class BaseFeature : IFeature
{
    private FeatureAttribute? _meta;

    private FeatureAttribute Meta =>
        _meta ??= GetType().GetCustomAttribute<FeatureAttribute>()
                  ?? throw new InvalidOperationException(
                      $"{GetType().Name} is missing [Feature] attribute");

    public Type? OwnerType { get; set; }
    
    public string OwnerKey =>
        OwnerType?.Name
        ?? throw new InvalidOperationException(
            $"{GetType().Name} has no owner assigned");

    public string FeatureKey => GetType().Name;

    public string Name => Loc.Instance[$"Features.{OwnerKey}.{FeatureKey}.Name"];
    public string Description => Loc.Instance[$"Features.{OwnerKey}.{FeatureKey}.Description"];
    public string Section
    {
        get
        {
            var section = Meta.GetSectionName();
            return string.IsNullOrEmpty(section) ? string.Empty : Loc.Instance[$"Features.{OwnerType?.Name}.Section.{section}"];
        }
    }

    protected virtual IEnumerable<RegistryToggle> RegistryToggles
        => [];

    public virtual Task<bool> GetStateAsync()
    {
        return Task.FromResult(RegistryToggles.All(t => t.GetState()));
    }

    public virtual Task EnableAsync() => SetTogglesState(true);

    public virtual Task DisableAsync() => SetTogglesState(false);

    private Task SetTogglesState(bool isOn)
    {
        foreach (var toggle in RegistryToggles)
            toggle.SetState(isOn);
        
        return Task.CompletedTask;
    }
}
