using System.Collections.ObjectModel;
using optimizerDuck.Core.Models.UI;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.Interfaces;

public interface IFeatureCategory
{
    public string Name { get; }
    public string Description { get; }
    public SymbolRegular Icon { get; init; }
    public FeatureCategoryOrder Order { get; init; }
    public ObservableCollection<IFeature> Features { get; init; }
}