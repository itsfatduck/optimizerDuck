using Wpf.Ui.Controls;

namespace optimizerDuck.Core.Interfaces;

public interface IFeature
{
    string Name { get; }
    string Description { get; }
    string Section { get; }
    public SymbolRegular Icon { get; }
    string FeatureKey { get; }

    Task<bool> GetStateAsync();
    Task EnableAsync();
    Task DisableAsync();
}