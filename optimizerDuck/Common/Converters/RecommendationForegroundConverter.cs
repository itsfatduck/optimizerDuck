using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Customize.Models;
using Wpf.Ui.Appearance;

namespace optimizerDuck.Common.Converters;

public static class ThemeBrushes
{
    public static Brush Primary =>
        ThemeResource.Get<Brush>("TextFillColorPrimaryBrush") ?? Brushes.White;

    public static Brush Inverse =>
        ThemeResource.Get<Brush>("TextFillColorInverseBrush") ?? Brushes.White;

    public static Brush Secondary =>
        ThemeResource.Get<Brush>("TextFillColorSecondaryBrush") ?? Brushes.Gray;
}

/// <summary>
///     Maps a recommendation state to a foreground brush; Depends follows the current theme,
///     and any missing or unrecognized input falls back to Primary.
/// </summary>
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
