using System.Windows;

namespace optimizerDuck.Core.Interfaces;

public interface IWindow
{
    event RoutedEventHandler Loaded;

    void Show();
}