using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using optimizerDuck.Common.Helpers;

namespace optimizerDuck.Common.Converters;

/// <summary>
///     Converts a byte size (long) to a color brush:
///     Green for small (&lt; 10 MB), Orange for medium (10-100 MB), Red for large (&gt; 100 MB).
/// </summary>
public class SizeToBrushConverter : IValueConverter
{
    private const long TenMB = 10L * 1024 * 1024;
    private const long HundredMB = 100L * 1024 * 1024;

    private static readonly SolidColorBrush GreenBrush =
        ThemeResource.Get<SolidColorBrush>("SystemFillColorSuccessBrush");

    private static readonly SolidColorBrush OrangeBrush =
        ThemeResource.Get<SolidColorBrush>("SystemFillColorCautionBrush");

    private static readonly SolidColorBrush RedBrush =
        ThemeResource.Get<SolidColorBrush>("SystemFillColorCriticalBrush");

    private static readonly SolidColorBrush DefaultBrush = new(Color.FromArgb(0x0, 0x0, 0x0, 0x0));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long bytes) return DefaultBrush;

        return bytes switch
        {
            <= 0 => DefaultBrush,
            < TenMB => GreenBrush,
            < HundredMB => OrangeBrush,
            _ => RedBrush
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}