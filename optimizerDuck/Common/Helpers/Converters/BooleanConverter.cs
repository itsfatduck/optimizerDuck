using System.Globalization;
using System.Windows.Data;

namespace optimizerDuck.Common.Helpers.Converters;

// Source - https://stackoverflow.com/a/5182660
// Posted by Atif Aziz, modified by community. See post 'Timeline' for change history
// Retrieved 2026-01-21, License - CC BY-SA 2.5

public class BooleanConverter<T>(T trueValue, T falseValue) : IValueConverter
{
    public T True { get; set; } = trueValue;
    public T False { get; set; } = falseValue;

    public virtual object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? True : False;
    }

    public virtual object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is T value1 && EqualityComparer<T>.Default.Equals(value1, True);
    }
}