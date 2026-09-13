using System.Globalization;
using System.Windows.Data;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Common.Converters;

/// <summary>Passes strings through; null or empty becomes localized Unknown.</summary>
public class NullToUnknownTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string) ? Loc.Instance["Common.Unknown"] : value!;
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
