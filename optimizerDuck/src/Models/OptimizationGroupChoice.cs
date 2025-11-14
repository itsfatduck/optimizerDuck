namespace optimizerDuck.Models;

public record struct OptimizationGroupChoice(string Name, OptimizationGroupOrder Order, List<OptimizationChoice> Optimizations);