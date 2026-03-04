namespace optimizerDuck.Core.Models.Attributes;

/// <summary>
///     Associates an optimization category with its corresponding UI page type
///     for automatic page registration.
/// </summary>
/// <param name="pageType">The <see cref="Type" /> of the XAML page for this category.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class OptimizationCategoryAttribute(Type pageType) : Attribute
{
    /// <summary>
    ///     The type of the UI page associated with this optimization category.
    /// </summary>
    public Type PageType { get; init; } = pageType;
}