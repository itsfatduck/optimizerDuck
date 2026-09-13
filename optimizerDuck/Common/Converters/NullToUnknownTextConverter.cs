using System.Globalization;
using System.Windows.Data;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Common.Converters;

/// <summary>Passes non-empty strings through; anything else becomes localized Unknown.</summary>
public class NullToUnknownTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string s && s.Length > 0 ? s : Loc.Instance["Common.Unknown"];
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
