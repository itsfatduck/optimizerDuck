using System.Collections.ObjectModel;
using optimizerDuck.Core.Models.UI;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.Interfaces;

public interface IToggleFeaturesCategory
{
    public string Name { get; }
    public string Description { get; }
    public SymbolRegular Icon { get; init; }
    public ToggleFeaturesCategoryOrder Order { get; init; }
    public ObservableCollection<IToggleFeature> Features { get; init; }
}