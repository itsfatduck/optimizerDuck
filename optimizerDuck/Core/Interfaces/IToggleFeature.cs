using optimizerDuck.Core.Models.UI;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.Interfaces;

public interface IToggleFeature
{
    string Name { get; }
    string Description { get; }
    OptimizationRisk Risk { get; }
    SymbolRegular Icon { get; }

    Task<bool> GetStateAsync();
    Task EnableAsync();
    Task DisableAsync();
}
