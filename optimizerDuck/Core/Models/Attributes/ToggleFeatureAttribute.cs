using optimizerDuck.Core.Models.UI;

namespace optimizerDuck.Core.Models.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ToggleFeatureAttribute : Attribute
{
    public required string Id { get; init; }
    public required OptimizationRisk Risk { get; init; }
    public ToggleFeatureType Type { get; init; } = ToggleFeatureType.Registry;
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ToggleFeatureCategoryAttribute : Attribute
{
    public Type? PageType { get; init; }
}
