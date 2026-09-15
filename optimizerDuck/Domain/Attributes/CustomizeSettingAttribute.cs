using optimizerDuck.Domain.Customize.Models;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CustomizeSettingAttribute : Attribute
{
    public string? Section { get; init; }
    public required SymbolRegular Icon { get; init; }
    public RecommendationState Recommendation { get; init; } = RecommendationState.None;

    /// <summary>
    ///     The <see cref="ICondition" /> implementation that must hold on the current system.
    ///     When <see langword="null" />, the setting is always available.
    /// </summary>
    public Type? Condition { get; init; }

    public string GetSectionName()
    {
        return Section ?? string.Empty;
    }
}
