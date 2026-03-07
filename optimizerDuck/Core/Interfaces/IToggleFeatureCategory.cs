using System.Collections.ObjectModel;
using optimizerDuck.Core.Models.UI;

namespace optimizerDuck.Core.Interfaces;

public interface IToggleFeatureCategory
{
    public string Name { get; init; }
    public ToggleFeatureCategoryOrder Order { get; init; }
    public ObservableCollection<IToggleFeature> Features { get; init; }
}

public enum ToggleFeatureCategoryOrder
{
    Privacy = 0,
    System = 1,
    AI = 2,
    UserExperience = 3
}
