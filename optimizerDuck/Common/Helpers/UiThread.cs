using System.Windows;
using System.Windows.Threading;

namespace optimizerDuck.Common.Helpers;

/// <summary>
///     Marshals work to the WPF UI thread, degrading gracefully to inline execution when
///     no <see cref="Application"/> exists (e.g. unit tests or headless hosts). Use this
///     instead of touching <c>Application.Current.Dispatcher</c> directly so background
///     threads never need to null-check the application instance.
/// </summary>
public static class UiThread
{
    /// <summary>
    ///     Runs <paramref name="action"/> on the UI thread. When already on the UI thread,
    ///     or when no <see cref="Application"/> exists, the action runs inline.
    /// </summary>
    public static Task InvokeAsync(
        Action action,
        DispatcherPriority priority = DispatcherPriority.Normal
    )
    {
        if (Application.Current?.Dispatcher is not { } dispatcher || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action, priority).Task;
    }

    /// <summary>
    ///     Runs <paramref name="action"/> on the UI thread and awaits its completion. When
    ///     already on the UI thread, or when no <see cref="Application"/> exists, the
    ///     action runs inline.
    /// </summary>
    public static Task InvokeAsync(
        Func<Task> action,
        DispatcherPriority priority = DispatcherPriority.Normal
    )
    {
        if (Application.Current?.Dispatcher is not { } dispatcher || dispatcher.CheckAccess())
            return action();

        return dispatcher.InvokeAsync(action, priority).Task.Unwrap();
    }
}
