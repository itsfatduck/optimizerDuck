using System.Collections.ObjectModel;
using optimizerDuck.Domain.UI;

namespace optimizerDuck.Domain.Abstractions;

/// <summary>
///     Defines a category that groups related optimizations together.
/// </summary>
public interface IOptimizationCategory
{
    /// <summary>
    ///     The localized display name of the category.
    /// </summary>
    public string Name { get; }

    public OptimizationCategoryOrder Order { get; init; }

    public ObservableCollection<IOptimization> Optimizations { get; init; }
}
