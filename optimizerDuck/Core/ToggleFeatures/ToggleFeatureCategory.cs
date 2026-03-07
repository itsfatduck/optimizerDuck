using System.Collections.ObjectModel;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.UI;

namespace optimizerDuck.Core.ToggleFeatures;

public class ToggleFeatureCategory : IToggleFeatureCategory
{
    public required string Name { get; init; }
    public ToggleFeatureCategoryOrder Order { get; init; }
    public ObservableCollection<IToggleFeature> Features { get; init; } = [];
}
