using System.Windows;

namespace optimizerDuck.Common.Converters;

/// <summary>Converts <c>true</c> to <see cref="Visibility.Visible"/> and <c>false</c> to <see cref="Visibility.Collapsed"/>.</summary>
public sealed class BooleanToVisibilityConverter()
    : BooleanConverter<Visibility>(Visibility.Visible, Visibility.Collapsed);
