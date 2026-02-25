using System.Globalization;
using System.Windows.Data;

namespace optimizerDuck.Common.Converters;

public class MinusNumberConverter : IValueConverter
{
    public int NumberToMinus { get; set; } = 1;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue)
            return intValue - NumberToMinus;
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}