using optimizerDuck.Domain.Customize.Models;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CustomizeSettingAttribute : Attribute
{
    public object? Section { get; init; }
    public required SymbolRegular Icon { get; init; }
    public RecommendationState Recommendation { get; init; } = RecommendationState.None;

    /// <summary>
    ///     The compatibility condition type (implementing <see cref="ICondition"/>)
    ///     that determines whether this setting is supported on the current system.
    ///     When <c>null</c>, the setting is always available.
    /// </summary>
    public Type? Condition { get; init; }

    public string GetSectionName()
    {
        if (Section == null)
            return string.Empty;

        return Section is Enum e
            ? Enum.GetName(e.GetType(), e) ?? string.Empty
            : Section.ToString() ?? string.Empty;
    }
}
