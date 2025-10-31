using Microsoft.Extensions.Logging;

namespace optimizerDuck.Interfaces;

public interface IOptimizationGroup
{
    public string Name { get; }
    public int Priority { get; }
    public static abstract ILogger Log { get; }
}