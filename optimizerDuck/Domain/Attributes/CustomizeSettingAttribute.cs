using optimizerDuck.Domain.Customize.Models;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CustomizeSettingAttribute : Attribute
{
    public object? Section { get; init; }
    public required SymbolRegular Icon { get; init; }
    public RecommendationState Recommendation { get; init; } = RecommendationState.None;

    public string GetSectionName()
    {
        if (Section == null)
            return string.Empty;

        return Section is Enum e
            ? Enum.GetName(e.GetType(), e) ?? string.Empty
            : Section.ToString() ?? string.Empty;
    }
}
