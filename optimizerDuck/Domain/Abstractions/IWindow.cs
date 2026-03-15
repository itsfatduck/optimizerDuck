using System.Windows;

namespace optimizerDuck.Domain.Abstractions;

/// <summary>
///     Defines the contract for a window that can be shown and observed for load events.
/// </summary>
public interface IWindow
{
    /// <summary>
    ///     Occurs when the window has been laid out, rendered, and is ready for interaction.
    /// </summary>
    event RoutedEventHandler Loaded;

    /// <summary>
    ///     Shows the window.
    /// </summary>
    void Show();
}