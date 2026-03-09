namespace optimizerDuck.Core.Models.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FeatureAttribute : Attribute
{
    public object? Section { get; init; }

    public string GetSectionName()
    {
        if (Section == null)
            return string.Empty;

        return Section is Enum e
            ? Enum.GetName(e.GetType(), e) ?? string.Empty
            : Section.ToString() ?? string.Empty;
    }
}