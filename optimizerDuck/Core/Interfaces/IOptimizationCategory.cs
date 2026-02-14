using System.Collections.ObjectModel;
using optimizerDuck.Core.Models.UI;

namespace optimizerDuck.Core.Interfaces;

public interface IOptimizationCategory
{
    public string Name { get; init; }
    public OptimizationCategoryOrder Order { get; init; }
    public ObservableCollection<IOptimization> Optimizations { get; init; }
}