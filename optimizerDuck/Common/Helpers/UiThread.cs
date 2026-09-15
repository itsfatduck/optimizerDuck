using System.Windows;
using System.Windows.Threading;

namespace optimizerDuck.Common.Helpers;

/// <summary>
///     Marshals work to the WPF UI thread, degrading to inline execution when no
///     <see cref="Application" /> exists, as in unit tests or headless hosts. Callers reach the
///     dispatcher through this type, so a background thread never null-checks the application.
/// </summary>
public static class UiThread
{
    /// <summary>
    ///     Runs <paramref name="action" /> on the UI thread. When already on the UI thread,
    ///     or when no <see cref="Application" /> exists, the action runs inline.
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
    ///     Runs <paramref name="action" /> on the UI thread and awaits its completion. When
    ///     already on the UI thread, or when no <see cref="Application" /> exists, the
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
