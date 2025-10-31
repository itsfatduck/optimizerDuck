namespace optimizerDuck.Models;

public record struct OptimizationGroupChoice(string Name, int Priority, List<OptimizationTweakChoice> Tweaks);