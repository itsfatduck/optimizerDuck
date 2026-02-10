namespace optimizerDuck.Core.Models.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class OptimizationCategoryAttribute(Type pageType) : Attribute
{
    public Type PageType { get; init; } = pageType;
}