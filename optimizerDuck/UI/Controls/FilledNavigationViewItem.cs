using Wpf.Ui.Controls;

namespace optimizerDuck.UI.Controls;

/// <summary>
///     Represents a NavigationViewItem that fills its SymbolIcon while active, regardless of the
///     NavigationViewPaneDisplayMode.
/// </summary>
public class FilledNavigationViewItem : NavigationViewItem
{
    public override void Activate(INavigationView navigationView)
    {
        base.Activate(navigationView);

        if (Icon is SymbolIcon symbolIcon)
            symbolIcon.Filled = true;
    }

    public override void Deactivate(INavigationView navigationView)
    {
        base.Deactivate(navigationView);

        if (Icon is SymbolIcon symbolIcon)
            symbolIcon.Filled = false;
    }
}
