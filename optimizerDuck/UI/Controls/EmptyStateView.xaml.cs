using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.Controls;

public partial class EmptyStateView : UserControl
{
    public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(
        nameof(Symbol),
        typeof(SymbolRegular),
        typeof(EmptyStateView),
        new PropertyMetadata(SymbolRegular.SearchInfo24)
    );

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(EmptyStateView),
        new PropertyMetadata(null)
    );

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(string),
        typeof(EmptyStateView),
        new PropertyMetadata(null)
    );

    public static readonly DependencyProperty ActionButtonTextProperty =
        DependencyProperty.Register(
            nameof(ActionButtonText),
            typeof(string),
            typeof(EmptyStateView),
            new PropertyMetadata(null)
        );

    public static readonly DependencyProperty ActionCommandProperty = DependencyProperty.Register(
        nameof(ActionCommand),
        typeof(ICommand),
        typeof(EmptyStateView),
        new PropertyMetadata(null)
    );

    public static readonly DependencyProperty ActionCommandParameterProperty =
        DependencyProperty.Register(
            nameof(ActionCommandParameter),
            typeof(object),
            typeof(EmptyStateView),
            new PropertyMetadata(null)
        );

    public EmptyStateView()
    {
        InitializeComponent();
    }

    public SymbolRegular Symbol
    {
        get => (SymbolRegular)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string? ActionButtonText
    {
        get => (string?)GetValue(ActionButtonTextProperty);
        set => SetValue(ActionButtonTextProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public object? ActionCommandParameter
    {
        get => GetValue(ActionCommandParameterProperty);
        set => SetValue(ActionCommandParameterProperty, value);
    }
}
