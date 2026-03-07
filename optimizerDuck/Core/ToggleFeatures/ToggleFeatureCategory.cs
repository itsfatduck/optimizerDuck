using System.Collections.ObjectModel;
using optimizerDuck.Core.Interfaces;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.ToggleFeatures;

public class ToggleFeatureCategory
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required SymbolRegular Icon { get; init; }
    public required Type PageType { get; init; }
    public ObservableCollection<IToggleFeature> Features { get; init; } = [];
}
