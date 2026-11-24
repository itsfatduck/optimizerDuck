using System.Collections.ObjectModel;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Domain.Optimizations.Models;

/// <summary>
///     Base class for optimization categories that discover nested optimizations and resolve
///     their display name from <c>Optimizer.&lt;CategoryName&gt;</c>.
/// </summary>
public abstract class OptimizationCategoryBase(OptimizationCategoryOrder order)
    : LocalizedObject,
        IOptimizationCategory
{
    /// <inheritdoc />
    public virtual string Name => Loc.Instance[$"Optimizer.{GetType().Name}"];

    /// <inheritdoc />
    public OptimizationCategoryOrder Order { get; init; } = order;

    /// <inheritdoc />
    public ObservableCollection<IOptimization> Optimizations { get; init; } = [];
}
