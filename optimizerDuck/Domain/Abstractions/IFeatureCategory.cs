using System.Collections.ObjectModel;
using optimizerDuck.Domain.UI;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Abstractions;

public interface IFeatureCategory
{
    public string Name { get; }
    public string Description { get; }
    public SymbolRegular Icon { get; init; }
    public FeatureCategoryOrder Order { get; init; }
    public ObservableCollection<IFeature> Features { get; init; }
}