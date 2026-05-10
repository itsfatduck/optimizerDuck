using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace optimizerDuck.Common.Converters;

public enum NumericComparisonType
{
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Equal,
    NotEqual
}

public class NumericComparisonToVisibilityConverter : IValueConverter
{
    public NumericComparisonType ComparisonType { get; set; } = NumericComparisonType.GreaterThan;

    public int Threshold { get; set; } = 0;

    public Visibility True { get; set; } = Visibility.Visible;

    public Visibility False { get; set; } = Visibility.Collapsed;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int intValue)
            return False;

        bool result = ComparisonType switch
        {
            NumericComparisonType.GreaterThan => intValue > Threshold,
            NumericComparisonType.GreaterThanOrEqual => intValue >= Threshold,
            NumericComparisonType.LessThan => intValue < Threshold,
            NumericComparisonType.LessThanOrEqual => intValue <= Threshold,
            NumericComparisonType.Equal => intValue == Threshold,
            NumericComparisonType.NotEqual => intValue != Threshold,
            _ => false,
        };

        return result ? True : False;
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
