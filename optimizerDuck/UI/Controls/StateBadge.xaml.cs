using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.Controls;

/// <summary>
///     Represents the chip that reports an item's state, as a control that answers the mouse so
///     it can be clicked to open the record of the last apply.
/// </summary>
public partial class StateBadge : UserControl
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(SymbolRegular),
        typeof(StateBadge),
        new PropertyMetadata(SymbolRegular.Circle24)
    );

    public static readonly DependencyProperty IconBrushProperty = DependencyProperty.Register(
        nameof(IconBrush),
        typeof(Brush),
        typeof(StateBadge),
        new PropertyMetadata(null)
    );

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(StateBadge),
        new PropertyMetadata(string.Empty)
    );

    public static readonly DependencyProperty IsPressedProperty = DependencyProperty.Register(
        nameof(IsPressed),
        typeof(bool),
        typeof(StateBadge),
        new PropertyMetadata(false)
    );

    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command),
        typeof(ICommand),
        typeof(StateBadge),
        new PropertyMetadata(null)
    );

    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
        nameof(CommandParameter),
        typeof(object),
        typeof(StateBadge),
        new PropertyMetadata(null)
    );

    public StateBadge()
    {
        InitializeComponent();
    }

    public SymbolRegular Icon
    {
        get => (SymbolRegular)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public Brush? IconBrush
    {
        get => (Brush?)GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    ///     Gets or sets a value that indicates whether the chip is pressed, which only changes
    ///     how it looks.
    /// </summary>
    public bool IsPressed
    {
        get => (bool)GetValue(IsPressedProperty);
        set => SetValue(IsPressedProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        IsPressed = true;
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        IsPressed = false;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // A press that ends outside the chip is not a click, so the press flag decides.
        if (!IsPressed)
            return;

        IsPressed = false;

        if (Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);
    }
}
