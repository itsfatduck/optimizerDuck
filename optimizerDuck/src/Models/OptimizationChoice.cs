using optimizerDuck.Interfaces;

namespace optimizerDuck.Models;

public record struct OptimizationChoice(
    IOptimization? Instance,
    string Name,
    string Description,
    bool EnabledByDefault);