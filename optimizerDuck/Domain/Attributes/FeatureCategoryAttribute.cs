namespace optimizerDuck.Domain.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class FeatureCategoryAttribute : Attribute
{
    public Type? PageType { get; init; }
}