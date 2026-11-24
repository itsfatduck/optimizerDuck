using System.Collections;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Windows;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.Behaviors;

/// <summary>
///     Synchronizes an <see cref="IEnumerable"/> source directly into
///     <see cref="NavigationView.MenuItems"/> instead of binding
///     <see cref="NavigationView.MenuItemsSource"/>.
/// </summary>
/// <remarks>
///     WPF-UI 4.3.0 drops <see cref="NotifyCollectionChangedAction.Remove"/> notifications in its
///     internal <c>MenuItemsSource</c> handler, leaving removed tabs visible. Mirroring changes into
///     <see cref="NavigationView.MenuItems"/> applies <c>ItemTemplate</c> on additions and honors
///     removals cleanly.
/// </remarks>
public static class NavigationViewMenuItemsBehavior
{
    private sealed class Subscription(
        INotifyCollectionChanged source,
        NotifyCollectionChangedEventHandler handler
    )
    {
        public void Detach() => source.CollectionChanged -= handler;
    }

    private static readonly ConditionalWeakTable<NavigationView, Subscription> Subscriptions = [];

    /// <summary>Identifies the <c>ItemsSource</c> attached dependency property.</summary>
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.RegisterAttached(
            "ItemsSource",
            typeof(IEnumerable),
            typeof(NavigationViewMenuItemsBehavior),
            new PropertyMetadata(null, OnItemsSourceChanged)
        );

    /// <summary>Gets the collection synchronized to <see cref="NavigationView.MenuItems"/>.</summary>
    public static IEnumerable? GetItemsSource(DependencyObject obj) =>
        (IEnumerable?)obj.GetValue(ItemsSourceProperty);

    /// <summary>Sets the collection synchronized to <see cref="NavigationView.MenuItems"/>.</summary>
    public static void SetItemsSource(DependencyObject obj, IEnumerable? value) =>
        obj.SetValue(ItemsSourceProperty, value);

    private static void OnItemsSourceChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        if (d is not NavigationView navView)
            return;

        if (Subscriptions.TryGetValue(navView, out var existing))
        {
            existing.Detach();
            Subscriptions.Remove(navView);
        }

        RebuildItems(navView, e.NewValue as IEnumerable);

        if (e.NewValue is not INotifyCollectionChanged notifySource)
            return;

        var weakNavView = new WeakReference<NavigationView>(navView);
        var capturedSource = e.NewValue as IEnumerable;
        NotifyCollectionChangedEventHandler? handler = null;
        handler = (_, args) =>
        {
            if (!weakNavView.TryGetTarget(out var target))
            {
                notifySource.CollectionChanged -= handler;
                return;
            }

            ApplyCollectionChange(target, capturedSource, args);
        };

        notifySource.CollectionChanged += handler;
        Subscriptions.Add(navView, new Subscription(notifySource, handler));
    }

    private static void RebuildItems(NavigationView navView, IEnumerable? source)
    {
        navView.MenuItems.Clear();
        if (source is null)
            return;

        foreach (var item in source)
            navView.MenuItems.Add(item);
    }

    private static void ApplyCollectionChange(
        NavigationView navView,
        IEnumerable? source,
        NotifyCollectionChangedEventArgs e
    )
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is not null:
                var insertAt = e.NewStartingIndex;
                foreach (var item in e.NewItems)
                {
                    if (insertAt >= 0 && insertAt <= navView.MenuItems.Count)
                        navView.MenuItems.Insert(insertAt++, item);
                    else
                        navView.MenuItems.Add(item);
                }
                break;

            case NotifyCollectionChangedAction.Remove when e.OldItems is not null:
                foreach (var item in e.OldItems)
                    navView.MenuItems.Remove(item);
                break;

            case NotifyCollectionChangedAction.Replace when e.NewItems is not null:
                if (
                    e.NewStartingIndex >= 0
                    && e.OldItems is { Count: 1 }
                    && e.NewItems.Count == 1
                    && e.NewStartingIndex < navView.MenuItems.Count
                )
                {
                    navView.MenuItems[e.NewStartingIndex] = e.NewItems[0];
                }
                else
                {
                    RebuildItems(navView, source);
                }
                break;

            case NotifyCollectionChangedAction.Move:
            case NotifyCollectionChangedAction.Reset:
            default:
                RebuildItems(navView, source);
                break;
        }
    }
}
