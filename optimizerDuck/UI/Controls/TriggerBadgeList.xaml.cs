using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace optimizerDuck.UI.Controls;

public partial class TriggerBadgeList : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(TriggerBadgeList),
        new PropertyMetadata(null)
    );

    public TriggerBadgeList()
    {
        InitializeComponent();
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }
}
