using System.Windows;

namespace optimizerDuck.Common.Helpers.Converters;

public sealed class BooleanToVisibilityConverter()
    : BooleanConverter<Visibility>(Visibility.Visible, Visibility.Collapsed);