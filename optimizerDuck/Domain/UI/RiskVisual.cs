using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.UI;

/// <summary>
///     Represents the visual display data for a risk level in the UI.
/// </summary>
public class RiskVisual
{
    /// <summary>
    ///     The localized display text for the risk level.
    /// </summary>
    public string Display { get; init; } = string.Empty;

    /// <summary>
    ///     The icon symbol to display for the risk level.
    /// </summary>
    public SymbolRegular Icon { get; init; }
}
