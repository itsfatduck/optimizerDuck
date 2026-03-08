[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ToggleFeatureCategoryAttribute : Attribute
{
    public Type? PageType { get; init; }
}