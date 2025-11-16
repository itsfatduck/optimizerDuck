using Microsoft.Extensions.Logging;
using optimizerDuck.Models;

namespace optimizerDuck.Interfaces;

public interface IOptimizationCategory
{
    public string Name { get; }
    public OptimizationCategoryOrder Order { get; }
    public static abstract ILogger Log { get; }
}