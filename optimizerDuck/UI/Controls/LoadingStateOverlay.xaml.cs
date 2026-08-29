using System.Windows;
using System.Windows.Controls;

namespace optimizerDuck.UI.Controls;

public partial class LoadingStateOverlay : UserControl
{
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
        nameof(IsLoading),
        typeof(bool),
        typeof(LoadingStateOverlay),
        new PropertyMetadata(false)
    );

    public static readonly DependencyProperty DimBackgroundProperty = DependencyProperty.Register(
        nameof(DimBackground),
        typeof(bool),
        typeof(LoadingStateOverlay),
        new PropertyMetadata(false)
    );

    public bool DimBackground
    {
        get => (bool)GetValue(DimBackgroundProperty);
        set => SetValue(DimBackgroundProperty, value);
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(LoadingStateOverlay),
        new PropertyMetadata(null)
    );

    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle),
        typeof(string),
        typeof(LoadingStateOverlay),
        new PropertyMetadata(null)
    );

    public LoadingStateOverlay()
    {
        InitializeComponent();
    }

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => (string?)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }
}
