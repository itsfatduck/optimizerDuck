using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Appearance;

namespace optimizerDuck.Common.Converters;

public sealed class ThemeToIndexConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ApplicationTheme.Dark => 1,
            ApplicationTheme.HighContrast => 2,
            _ => 0
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            1 => ApplicationTheme.Dark,
            2 => ApplicationTheme.HighContrast,
            _ => ApplicationTheme.Light
        };
    }
}