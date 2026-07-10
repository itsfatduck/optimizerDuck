namespace optimizerDuck.Services.System;

/// <summary>
///     Defines a compile-time-safe token for cross-page data change events.
/// </summary>
/// <remarks>
///     Create a new event by declaring a stateless record that implements this interface:
///     <code language="csharp">public readonly record struct MyEvent : ICrossPageEvent;</code>
///     Use it with <see cref="CrossPageEventBus.NotifyDataChanged{T}"/> and
///     <see cref="CrossPageEventBus.HasPendingRefresh{T}"/>.
/// </remarks>
public interface ICrossPageEvent;

/// <summary>
///     Signals that scheduled tasks were added, removed, or toggled.
/// </summary>
/// <remarks>
///     Fired by <see cref="optimizerDuck.UI.ViewModels.Pages.ScheduledTasksViewModel"/> after toggling a task.
///     Consumed by <see cref="optimizerDuck.UI.ViewModels.Pages.StartupManagerViewModel"/> to reload its Tasks section.
/// </remarks>
public readonly record struct ScheduledTasksChanged : ICrossPageEvent;

/// <summary>
///     Signals that startup apps or tasks were added, removed, or toggled.
/// </summary>
/// <remarks>
///     Fired by <see cref="optimizerDuck.UI.ViewModels.Pages.StartupManagerViewModel"/> after toggling a startup task.
///     Consumed by <see cref="optimizerDuck.UI.ViewModels.Pages.ScheduledTasksViewModel"/> to reload all tasks.
/// </remarks>
public readonly record struct StartupAppsChanged : ICrossPageEvent;

/// <summary>
///     Signals that bloatware packages were removed or may have changed externally.
/// </summary>
/// <remarks>
///     Fired by <see cref="optimizerDuck.UI.ViewModels.Pages.BloatwareViewModel"/> after removing packages.
///     Consumed by <see cref="optimizerDuck.UI.ViewModels.Pages.StartupManagerViewModel"/> (reloads tasks)
///     and <see cref="optimizerDuck.UI.ViewModels.Pages.BloatwareViewModel"/> (full refresh on re-navigation).
/// </remarks>
public readonly record struct BloatwareChanged : ICrossPageEvent;
