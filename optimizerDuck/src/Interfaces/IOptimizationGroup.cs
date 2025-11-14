using Microsoft.Extensions.Logging;
using optimizerDuck.Models;

namespace optimizerDuck.Interfaces;

public interface IOptimizationGroup
{
    public string Name { get; }
    public OptimizationGroupOrder Order { get; }
    public static abstract ILogger Log { get; }
}