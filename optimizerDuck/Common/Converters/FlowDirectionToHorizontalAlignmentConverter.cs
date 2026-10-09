using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace optimizerDuck.Common.Converters;

/// <summary>
///     Maps the UI flow direction to the horizontal edge that content lines up with.
/// </summary>
/// <remarks>
///     WPF mirrors text direction and <see cref="TextBlock.TextAlignment" />, but it never mirrors layout:
///     <see cref="HorizontalAlignment" /> is absolute. A row that has to hug the reading start edge has to be told
///     which edge that is for the current language.
/// </remarks>
public class FlowDirectionToHorizontalAlignmentConverter : IValueConverter
{
    /// <summary>
    ///     When <see langword="true" />, maps to the far edge instead of the reading start edge.
    /// </summary>
    public bool ToEndEdge { get; set; }

    /// <summary>
    ///     Converts a <see cref="FlowDirection" /> to the matching <see cref="HorizontalAlignment" />.
    /// </summary>
    /// <param name="value">The flow direction, normally <c>Loc.Instance.Direction</c>.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Left for LTR and Right for RTL, or the opposite when <see cref="ToEndEdge" /> is set.</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isRightToLeft = value is FlowDirection.RightToLeft;
        if (ToEndEdge)
            return isRightToLeft ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        return isRightToLeft ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    }

    /// <summary>Not supported; the converter is one way.</summary>
    /// <param name="value">Unused.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}
