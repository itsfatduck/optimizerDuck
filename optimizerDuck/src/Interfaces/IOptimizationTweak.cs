using optimizerDuck.Core.Services;
using optimizerDuck.Models;

namespace optimizerDuck.Interfaces;

public interface IOptimizationTweak
{
    public string Name { get; }
    public string Description { get; }
    public bool EnabledByDefault { get; }
    public OptimizationImpact Impact { get; }
    public Task Apply(SystemSnapshot s);
}