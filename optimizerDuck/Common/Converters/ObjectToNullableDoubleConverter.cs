using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace optimizerDuck.Common.Converters;

public sealed class ObjectToNullableDoubleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return null;

        if (value is double d)
            return d;

        if (value is float f)
            return (double)f;

        if (value is int i)
            return (double)i;

        if (value is long l)
            return (double)l;

        if (value is short sh)
            return (double)sh;

        if (value is byte b)
            return (double)b;

        if (value is decimal dec)
            return (double)dec;

        if (value is string s)
        {
            if (double.TryParse(s, NumberStyles.Any, culture, out var parsed))
                return parsed;
            return DependencyProperty.UnsetValue;
        }

        if (value.GetType() == typeof(object))
            return null;

        if (value is bool)
            return DependencyProperty.UnsetValue;

        if (value is IConvertible convertible)
        {
            try
            {
                return System.Convert.ToDouble(convertible, culture);
            }
            catch
            {
                return DependencyProperty.UnsetValue;
            }
        }

        return DependencyProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return null;

        var isInt =
            parameter is string p && p.Equals("Int", StringComparison.OrdinalIgnoreCase);

        if (value is double db)
            return isInt ? (int)Math.Round(db) : db;

        if (value is float fb)
            return isInt ? (int)Math.Round(fb) : (double)fb;

        if (value is string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return null;

            if (isInt)
            {
                if (int.TryParse(str, NumberStyles.Any, culture, out var intParsed))
                    return intParsed;
                if (double.TryParse(str, NumberStyles.Any, culture, out var dblParsed))
                    return (int)Math.Round(dblParsed);
                return DependencyProperty.UnsetValue;
            }

            if (double.TryParse(str, NumberStyles.Any, culture, out var dbl))
                return dbl;

            return DependencyProperty.UnsetValue;
        }

        return value;
    }
}
