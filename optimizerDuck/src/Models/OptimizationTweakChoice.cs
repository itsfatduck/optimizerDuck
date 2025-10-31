using optimizerDuck.Interfaces;

namespace optimizerDuck.Models;

public record struct OptimizationTweakChoice(
    IOptimizationTweak? Instance,
    string Name,
    string? Description,
    bool EnabledByDefault);