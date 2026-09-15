using System.Globalization;
using System.Windows.Data;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Common.Converters;

/// <summary>
///     Displays a system-info enum through the resource key <c>Enum.{Type}.{Value}</c>. An
///     <c>Unknown</c> value and a null map to <c>Common.Unknown</c>, and a missing resource falls
///     back to the raw value name, not the key. Re-evaluates on language change.
/// </summary>
public class EnumDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return Loc.Instance["Common.Unknown"];

        if (value is Enum e)
        {
            if (e.ToString() == "Unknown")
                return Loc.Instance["Common.Unknown"];
            var key = $"Enum.{e.GetType().Name}.{e}";
            var text = Loc.Instance[key];
            return text == key ? e.ToString() : text;
        }

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
