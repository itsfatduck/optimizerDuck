using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using optimizerDuck.Common.Helpers;

namespace optimizerDuck.Common.Converters;

/// <summary>Maps a progress percentage to green below 50%, yellow below 80%, red above.</summary>
public class ProgressBarValueToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush DefaultBrush = CreateFrozenBrush(
        Color.FromArgb(0x0, 0x0, 0x0, 0x0)
    );

    private static readonly SolidColorBrush SuccessFallbackBrush = CreateFrozenBrush(
        Color.FromRgb(0, 180, 0)
    );

    private static readonly SolidColorBrush CautionFallbackBrush = CreateFrozenBrush(
        Color.FromRgb(255, 140, 0)
    );

    private static readonly SolidColorBrush CriticalFallbackBrush = CreateFrozenBrush(
        Color.FromRgb(220, 0, 0)
    );

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double percent)
            return DefaultBrush;

        return percent switch
        {
            < 0 => DefaultBrush,
            < 50 => ThemeResource.Get<Brush>("SystemFillColorSuccessBrush")
                ?? SuccessFallbackBrush,
            < 80 => ThemeResource.Get<Brush>("SystemFillColorCautionBrush")
                ?? CautionFallbackBrush,
            _ => ThemeResource.Get<Brush>("SystemFillColorCriticalBrush") ?? CriticalFallbackBrush,
        };
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    )
    {
        throw new NotImplementedException();
    }
}
