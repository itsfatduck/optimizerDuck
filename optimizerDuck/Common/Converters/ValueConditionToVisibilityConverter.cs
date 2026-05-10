using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace optimizerDuck.Common.Converters;

public enum ValueConditionType
{
    IsNull,
    IsNotNull,
    IsZero,
    IsNotZero,
    IsGreaterThanZero,
    IsLessThanZero
}

public class ValueConditionToVisibilityConverter : IValueConverter
{
    public ValueConditionType ConditionType { get; set; } = ValueConditionType.IsNull;

    public Visibility True { get; set; } = Visibility.Visible;

    public Visibility False { get; set; } = Visibility.Collapsed;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool result = ConditionType switch
        {
            ValueConditionType.IsNull => value is null,
            ValueConditionType.IsNotNull => value is not null,
            ValueConditionType.IsZero => value is 0,
            ValueConditionType.IsNotZero => value is not 0,
            ValueConditionType.IsGreaterThanZero => value is > 0,
            ValueConditionType.IsLessThanZero => value is < 0,
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
