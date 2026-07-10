namespace optimizerDuck.Services.System;

/// <summary>
///     Provides a thread-safe event bus for signaling cross-page data changes.
/// </summary>
/// <remarks>
///     When one ViewModel modifies data that another page displays, call <see cref="NotifyDataChanged{T}"/>
///     with the appropriate event type. The target ViewModel checks for pending changes in
///     <c>OnNavigatedToAsync</c> via <see cref="HasPendingRefresh{T}"/> to reload stale data.
///     <para>
///         Internally uses a <see cref="HashSet{T}"/> of <see cref="Type"/> keys behind a lock.
///         All type safety is enforced at compile time via the <see cref="ICrossPageEvent"/> constraint.
///     </para>
/// </remarks>
public static class CrossPageEventBus
{
    private static readonly HashSet<Type> _pendingEvents = [];

    /// <summary>
    ///     Adds a pending refresh notification for the specified event type.
    /// </summary>
    /// <typeparam name="T">An event type that implements <see cref="ICrossPageEvent"/>.</typeparam>
    /// <remarks>
    ///     The target page picks up this notification the next time it navigates to.
    ///     Multiple notifications for the same type are coalesced into one.
    /// </remarks>
    public static void NotifyDataChanged<T>()
        where T : ICrossPageEvent
    {
        lock (_pendingEvents)
        {
            _pendingEvents.Add(typeof(T));
        }
    }

    /// <summary>
    ///     Checks and consumes a pending refresh notification for the specified event type.
    /// </summary>
    /// <typeparam name="T">An event type that implements <see cref="ICrossPageEvent"/>.</typeparam>
    /// <returns>
    ///     <see langword="true"/> if a refresh was pending; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///     This method removes the notification so subsequent calls return <see langword="false"/>
    ///     until a new <see cref="NotifyDataChanged{T}"/> call arrives.
    /// </remarks>
    public static bool HasPendingRefresh<T>()
        where T : ICrossPageEvent
    {
        lock (_pendingEvents)
        {
            return _pendingEvents.Remove(typeof(T));
        }
    }
}
