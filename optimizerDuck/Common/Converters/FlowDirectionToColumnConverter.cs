using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace optimizerDuck.Common.Converters;

/// <summary>
///     Maps the UI flow direction to a grid column index, so two children can swap sides per language.
/// </summary>
/// <remarks>
///     WPF does not mirror <see cref="Grid" /> columns, so a two column row that has to read from the right in a
///     right to left language has to place its children itself. Register this converter twice: once for the child
///     that starts the row and once with <see cref="Invert" /> for the child that follows it.
/// </remarks>
public class FlowDirectionToColumnConverter : IValueConverter
{
    /// <summary>
    ///     When <see langword="true" />, returns the complementary column for the child that moves the other way.
    /// </summary>
    public bool Invert { get; set; }

    /// <summary>
    ///     Converts a <see cref="FlowDirection" /> to the column index the child belongs in.
    /// </summary>
    /// <param name="value">The flow direction, normally <c>Loc.Instance.Direction</c>.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>0 for LTR and 1 for RTL, or the complementary column when <see cref="Invert" /> is set.</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var column = value is FlowDirection.RightToLeft ? 1 : 0;
        return Invert ? 1 - column : column;
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
