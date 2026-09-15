using System.Globalization;
using System.Windows.Data;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Common.Converters;

/// <summary>Formats a positive megabyte count as a GB string; anything else is Unknown.</summary>
public class MBToGBConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int mb and > 0)
            return (mb / 1024.0).ToString("F1", culture);
        return Loc.Instance["Common.Unknown"];
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
