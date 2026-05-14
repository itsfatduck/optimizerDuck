using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace optimizerDuck.Common.Converters;

public class ProgressBarVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return Visibility.Collapsed;

        var total = 0d;
        var isIndeterminate = false;

        if (values[0] is IConvertible convertible)
            total = convertible.ToDouble(culture);

        if (values[1] is bool b)
            isIndeterminate = b;

        return total > 0 || isIndeterminate ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object parameter,
        CultureInfo culture
    )
    {
        throw new NotSupportedException();
    }
}
