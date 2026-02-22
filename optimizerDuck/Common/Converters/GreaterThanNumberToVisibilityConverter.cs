using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace optimizerDuck.Common.Helpers.Converters;

public class GreaterThanNumberToVisibilityConverter : IValueConverter
{
    public int Threshold { get; set; } = 1;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int intValue)
            return Visibility.Collapsed;

        return intValue > Threshold ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}