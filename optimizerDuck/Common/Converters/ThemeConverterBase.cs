using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Appearance;

namespace optimizerDuck.Common.Converters;

public abstract class ThemeConverterBase<T> : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ApplicationTheme theme)
            return ConvertTheme(theme, parameter);

        return GetDefault();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    protected abstract T? ConvertTheme(ApplicationTheme theme, object parameter);

    protected virtual T? GetDefault() => default;
}
