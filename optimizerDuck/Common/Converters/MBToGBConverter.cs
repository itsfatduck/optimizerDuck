using System.Globalization;
using System.Windows.Data;

namespace optimizerDuck.Common.Converters;

/// <summary>Converts a megabyte value to a formatted gigabyte string, or "N/A" when invalid.</summary>
public class MBToGBConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int mb and > 0)
            return (mb / 1024.0).ToString("F1");
        return "N/A";
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
