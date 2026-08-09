using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Customize.Models;
using Wpf.Ui.Appearance;

namespace optimizerDuck.Common.Converters;

/// <summary>Provides theme-aware text brushes for converter use.</summary>
public static class ThemeBrushes
{
    public static Brush Primary => Clone("TextFillColorPrimaryBrush");

    public static Brush Inverse => Clone("TextFillColorInverseBrush");

    public static Brush Secondary => Clone("TextFillColorSecondaryBrush");

    private static Brush Clone(string key)
    {
        var brush = ThemeResource.Get<SolidColorBrush>(key);
        if (brush != null)
            return brush.Clone();

        return Brushes.White;
    }
}

/// <summary>Picks a foreground brush for a recommendation state based on the current app theme.</summary>
public class RecommendationForegroundConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return ThemeBrushes.Primary;

        if (values[0] is not RecommendationState state)
            return ThemeBrushes.Primary;

        if (values[1] is not ApplicationTheme theme)
            return ThemeBrushes.Primary;

        var isDark = theme == ApplicationTheme.Dark;

        return state switch
        {
            RecommendationState.On => ThemeBrushes.Inverse,

            RecommendationState.Off => ThemeBrushes.Primary,

            RecommendationState.Depends => isDark ? ThemeBrushes.Inverse : ThemeBrushes.Primary,

            _ => ThemeBrushes.Inverse,
        };
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
