using Wpf.Ui.Controls;

namespace optimizerDuck.UI.Controls;

/// <summary>
///     A custom NavigationViewItem that automatically fills its SymbolIcon when active,
///     regardless of the NavigationViewPaneDisplayMode.
/// </summary>
public class FilledNavigationViewItem : NavigationViewItem
{
    public override void Activate(INavigationView navigationView)
    {
        base.Activate(navigationView);

        if (Icon is SymbolIcon symbolIcon) symbolIcon.Filled = true;
    }

    public override void Deactivate(INavigationView navigationView)
    {
        base.Deactivate(navigationView);

        if (Icon is SymbolIcon symbolIcon) symbolIcon.Filled = false;
    }
}