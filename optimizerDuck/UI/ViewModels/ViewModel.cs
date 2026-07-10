using CommunityToolkit.Mvvm.ComponentModel;
using Wpf.Ui.Abstractions.Controls;

namespace optimizerDuck.UI.ViewModels;

/// <summary>
///     Provides a base class for view models that support navigation and validation.
/// </summary>
/// <remarks>
///     <see cref="ViewModel"/> implements <see cref="INavigationAware"/> to participate in
///     navigation events. It also inherits from <see cref="ObservableValidator"/> for property
///     change notification and validation support.
///     <para>
///         Override <see cref="InitializeOnceAsync"/> to run one-time setup logic.
///         Override <see cref="OnNavigatedToAsync"/> and <see cref="OnNavigatedFromAsync"/>
///         to react to page transitions.
///     </para>
/// </remarks>
public abstract class ViewModel : ObservableValidator, INavigationAware
{
    private bool _isInitialized;

    /// <summary>
    ///     Gets a value that indicates whether the one-time initialization has completed.
    /// </summary>
    /// <value>
    ///     <see langword="true"/> if <see cref="InitializeOnceAsync"/> has finished;
    ///     otherwise <see langword="false"/>.
    /// </value>
    protected bool IsInitialized => _isInitialized;

    /// <summary>
    ///     Runs one-time initialization logic the first time the page is navigated to.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    ///     Override this method to load data or subscribe to events. It runs only once
    ///     per view model lifetime, even if the user navigates away and back.
    /// </remarks>
    protected virtual Task InitializeOnceAsync() => Task.CompletedTask;

    /// <summary>
    ///     Called when the page becomes the active view.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    ///     Ensures <see cref="InitializeOnceAsync"/> runs exactly once, then calls
    ///     <see cref="OnNavigatedTo()"/>. If initialization fails, the flag resets
    ///     so the next navigation can retry.
    /// </remarks>
    public virtual async Task OnNavigatedToAsync()
    {
        if (!_isInitialized)
        {
            _isInitialized = true;
            try
            {
                await InitializeOnceAsync();
            }
            catch
            {
                // Reset flag so a future navigation can retry initialization
                _isInitialized = false;
                throw;
            }
        }

        OnNavigatedTo();
    }

    /// <summary>
    ///     Called when the page is no longer the active view.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    ///     Override this method to clean up resources or stop timers when leaving the page.
    /// </remarks>
    public virtual Task OnNavigatedFromAsync()
    {
        OnNavigatedFrom();

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Called after the page becomes the active view.
    /// </summary>
    /// <remarks>
    ///     Override this method for synchronous logic that runs after every navigation,
    ///     not just the first one.
    /// </remarks>
    public virtual void OnNavigatedTo() { }

    /// <summary>
    ///     Called when the page is no longer the active view.
    /// </summary>
    /// <remarks>
    ///     Override this method for synchronous cleanup logic that runs on every navigation away.
    /// </remarks>
    public virtual void OnNavigatedFrom() { }
}
