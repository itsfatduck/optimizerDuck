using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Services.Managers;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.ToggleFeatures;

public abstract class BaseToggleFeature : IToggleFeature
{
    protected RegistryToggle? Toggle { get; init; }

    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract OptimizationRisk Risk { get; }
    public abstract SymbolRegular Icon { get; }

    public virtual async Task<bool> GetStateAsync()
    {
        if (Toggle == null)
            return false;
        return await Task.FromResult(Toggle.GetState());
    }

    public virtual async Task EnableAsync()
    {
        if (Toggle != null)
            Toggle.SetState(true);
        await Task.CompletedTask;
    }

    public virtual async Task DisableAsync()
    {
        if (Toggle != null)
            Toggle.SetState(false);
        await Task.CompletedTask;
    }

    public string LocalizedName => Loc.Instance[$"ToggleFeature.{GetType().Name}.Name"];
    public string LocalizedDescription => Loc.Instance[$"ToggleFeature.{GetType().Name}.Description"];
}
