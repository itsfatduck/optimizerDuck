using System.Reflection;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Services.Managers;
using optimizerDuck.Services.OptimizationServices;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Features.Models;

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

    protected virtual IEnumerable<RegistryToggle> RegistryToggles
        => [];

    public string FeatureKey => GetType().Name;

    public string Name => Loc.Instance[$"Features.{OwnerKey}.{FeatureKey}.Name"];
    public string Description => Loc.Instance[$"Features.{OwnerKey}.{FeatureKey}.Description"];

    public string Section
    {
        get
        {
            var section = Meta.GetSectionName();
            return string.IsNullOrEmpty(section)
                ? string.Empty
                : Loc.Instance[$"Features.{OwnerType?.Name}.Section.{section}"];
        }
    }

    public SymbolRegular Icon => Meta.Icon;

    public virtual Task<bool> GetStateAsync()
    {
        return Task.FromResult(RegistryToggles.All(t => t.GetState()));
    }

    protected virtual bool NeedsPostAction => false;

    public virtual async Task EnableAsync()
    {
        await SetTogglesState(true);

        if (NeedsPostAction)
            await ExecutePostActionAsync();
    }

    public virtual async Task DisableAsync()
    {
        await SetTogglesState(false);

        if (NeedsPostAction)
            await ExecutePostActionAsync();
    }

    protected virtual Task ExecutePostActionAsync()
    {
        // Default post-action: Restart Explorer
        ShellService.CMD("taskkill /f /im explorer.exe & start explorer.exe");
        return Task.CompletedTask;
    }

    private Task SetTogglesState(bool isOn)

    {
        foreach (var toggle in RegistryToggles)
            toggle.SetState(isOn);

        return Task.CompletedTask;
    }
}