using optimizerDuck.Core.Services;

namespace optimizerDuck.Interfaces;

public interface IOptimizationTweak
{
    public string Name { get; }
    public string Description { get; }
    public bool EnabledByDefault { get; }
    public Task Apply(SystemSnapshot s);
}