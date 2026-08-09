using System.Windows;

namespace optimizerDuck.Common.Converters;

/// <summary>Converts <c>true</c> to <see cref="Visibility.Collapsed"/> and <c>false</c> to <see cref="Visibility.Visible"/>.</summary>
public sealed class InverseBooleanToVisibilityConverter()
    : BooleanConverter<Visibility>(Visibility.Collapsed, Visibility.Visible);
