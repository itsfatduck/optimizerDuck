using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using optimizerDuck.Common.Helpers;

namespace optimizerDuck.Common.Converters;

public class ProgressBarValueToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBrush =
        ThemeResource.Get<SolidColorBrush>("SystemFillColorSuccessBrush")
        ?? new SolidColorBrush(Color.FromRgb(0, 180, 0));

    private static readonly SolidColorBrush YellowBrush =
        ThemeResource.Get<SolidColorBrush>("SystemFillColorCautionBrush")
        ?? new SolidColorBrush(Color.FromRgb(255, 140, 0));

    private static readonly SolidColorBrush RedBrush =
        ThemeResource.Get<SolidColorBrush>("SystemFillColorCriticalBrush")
        ?? new SolidColorBrush(Color.FromRgb(220, 0, 0));

    private static readonly SolidColorBrush DefaultBrush = new(Color.FromArgb(0x0, 0x0, 0x0, 0x0));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double percent)
            return DefaultBrush;

        return percent switch
        {
            < 0 => DefaultBrush,
            < 50 => GreenBrush,
            < 80 => YellowBrush,
            _ => RedBrush,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
