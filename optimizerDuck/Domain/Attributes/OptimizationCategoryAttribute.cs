namespace optimizerDuck.Domain.Attributes;

/// <summary>
///     Associates an optimization category with its corresponding UI page type
///     for automatic page registration.
/// </summary>
/// <param name="pageType">The <see cref="Type" /> of the XAML page for this category.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class OptimizationCategoryAttribute(Type pageType) : Attribute
{
    public Type PageType { get; init; } = pageType;
}
