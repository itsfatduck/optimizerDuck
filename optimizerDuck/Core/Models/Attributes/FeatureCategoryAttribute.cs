namespace optimizerDuck.Core.Models.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class FeatureCategoryAttribute : Attribute
{
    public Type? PageType { get; init; }
}