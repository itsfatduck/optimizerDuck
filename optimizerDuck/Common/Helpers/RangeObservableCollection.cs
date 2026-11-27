using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace optimizerDuck.Common.Helpers;

/// <summary>
///     An <see cref="ObservableCollection{T}" /> supporting atomic batch replacement with a single
///     <see cref="NotifyCollectionChangedAction.Reset" /> notification instead of per-item events.
/// </summary>
public class RangeObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification;

    /// <summary>
    ///     Replaces all elements in the collection with <paramref name="items" /> while raising a single reset notification.
    /// </summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        CheckReentrancy();
        _suppressNotification = true;
        try
        {
            Items.Clear();
            foreach (var item in items)
                Items.Add(item);
        }
        finally
        {
            _suppressNotification = false;
        }

        OnPropertyChanged(new PropertyChangedEventArgs("Count"));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotification)
            base.OnCollectionChanged(e);
    }
}
