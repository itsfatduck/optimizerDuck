using System.Collections.ObjectModel;
using optimizerDuck.Core.Models.UI;

namespace optimizerDuck.Core.Interfaces;

/// <summary>
///     Defines a category that groups related optimizations together.
/// </summary>
public interface IOptimizationCategory
{
    /// <summary>
    ///     The localized display name of the category.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    ///     The display order of this category in the UI.
    /// </summary>
    public OptimizationCategoryOrder Order { get; init; }

    /// <summary>
    ///     The collection of optimizations belonging to this category.
    /// </summary>
    public ObservableCollection<IOptimization> Optimizations { get; init; }
}