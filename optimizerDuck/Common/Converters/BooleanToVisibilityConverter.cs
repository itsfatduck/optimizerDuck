using System.Windows;

namespace optimizerDuck.Common.Converters;

public sealed class BooleanToVisibilityConverter()
    : BooleanConverter<Visibility>(Visibility.Visible, Visibility.Collapsed);