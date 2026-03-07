using System.Windows;

namespace optimizerDuck.Common.Converters;

public sealed class InverseBooleanToVisibilityConverter()
    : BooleanConverter<Visibility>(Visibility.Collapsed, Visibility.Visible);
