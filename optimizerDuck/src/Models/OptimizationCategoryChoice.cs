namespace optimizerDuck.Models;

public record struct OptimizationCategoryChoice(string Name, OptimizationCategoryOrder Order, List<OptimizationChoice> Optimizations);