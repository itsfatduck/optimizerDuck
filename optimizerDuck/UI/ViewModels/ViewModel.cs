using CommunityToolkit.Mvvm.ComponentModel;
using Wpf.Ui.Abstractions.Controls;

namespace optimizerDuck.UI.ViewModels;

/// <summary>
///     Provides a base class for view models that supports navigation event handling and validation.
/// </summary>
/// <remarks>
///     Inherit from this class to implement view models that participate in navigation events within an
///     application. The ViewModel class implements the INavigationAware interface and provides virtual methods for
///     responding to navigation events, allowing derived classes to override and customize navigation behavior as needed.
///     It also inherits from ObservableValidator to support property validation and change notification.
/// </remarks>
public abstract class ViewModel : ObservableValidator, INavigationAware
{
    private bool _isInitialized;

    protected bool IsInitialized => _isInitialized;

    protected virtual Task InitializeOnceAsync() => Task.CompletedTask;

    public virtual async Task OnNavigatedToAsync()
    {
        if (!_isInitialized)
        {
            await InitializeOnceAsync();
            _isInitialized = true;
        }

        OnNavigatedTo();
    }

    public virtual Task OnNavigatedFromAsync()
    {
        OnNavigatedFrom();

        return Task.CompletedTask;
    }

    public virtual void OnNavigatedTo() { }

    public virtual void OnNavigatedFrom() { }
}
