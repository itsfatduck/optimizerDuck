using optimizerDuck.Core.Models.UI;

namespace optimizerDuck.Core.Models.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class OptimizationAttribute : Attribute
{
    public required string Id { get; init; }
    public required OptimizationRisk Risk { get; init; }
    public required OptimizationTags Tags { get; init; }
}