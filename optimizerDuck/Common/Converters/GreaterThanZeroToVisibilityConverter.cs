using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace optimizerDuck.Common.Converters;

public class GreaterThanZeroToVisibilityConverter : IValueConverter
{
    public Visibility True { get; set; } = Visibility.Visible;
    public Visibility False { get; set; } = Visibility.Collapsed;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is > 0 ? True : False;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}