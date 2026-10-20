using System.Collections.ObjectModel;
using optimizerDuck.Domain.UI;

namespace optimizerDuck.Domain.Abstractions;

/// <summary>
///     Defines a category that groups related optimizations together.
/// </summary>
public interface IOptimizationCategory
{
    /// <summary>Gets the localized display name of the category.</summary>
    public string Name { get; }

    /// <summary>Gets the display order index used to sort categories.</summary>
    public OptimizationCategoryOrder Order { get; init; }

    /// <summary>Gets the collection of optimizations contained in this category.</summary>
    public ObservableCollection<IOptimization> Optimizations { get; init; }
}
