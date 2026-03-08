using System.Reflection;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Services.Managers;
using Wpf.Ui.Controls;

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

    public virtual Task<bool> GetStateAsync()
    {
        return Task.FromResult(false);
    }

    public virtual Task EnableAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task DisableAsync()
    {
        return Task.CompletedTask;
    }
}
