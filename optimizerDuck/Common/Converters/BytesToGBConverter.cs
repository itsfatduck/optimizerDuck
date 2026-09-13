using System.Globalization;
using System.Windows.Data;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Common.Converters;

/// <summary>Formats a byte count as gigabytes ("F1"). Null maps to localized Unknown.</summary>
public class BytesToGBConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double bytes = value switch
        {
            long l => l,
            int i => i,
            double d => d,
            _ => double.NaN,
        };
        if (double.IsNaN(bytes) || bytes < 0)
            return Loc.Instance["Common.Unknown"];
        return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F1", culture);
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
