using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace optimizerDuck.Common.Converters;

public class EmptyWhileNotLoadingConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return Visibility.Collapsed;

        if (values[0] is int count && values[1] is bool isLoading)
        {
            return (count == 0 && !isLoading)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}