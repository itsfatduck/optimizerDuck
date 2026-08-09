using System.Globalization;
using System.Windows.Data;

namespace optimizerDuck.Common.Converters;

/// <summary>Maps a container width to a responsive column count (1 to 4).</summary>
public class WidthToColumnConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double width)
            return 2;
        return width switch
        {
            < 770 => 1,
            < 1100 => 2,
            < 1480 => 3,
            _ => 4,
        };
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    )
    {
        throw new NotSupportedException();
    }
}
