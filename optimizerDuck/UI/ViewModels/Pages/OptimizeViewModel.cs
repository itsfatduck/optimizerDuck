using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.Optimization;
using optimizerDuck.UI.Pages;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class OptimizeViewModel : ViewModel
{
    private readonly OptimizationRegistry _optimizationRegistry;
    private readonly Dispatcher? _dispatcher = Dispatcher.FromThread(Thread.CurrentThread);
    private NavigationViewItem? _legacyNavItem;
    private bool _categoriesLoaded;

    [ObservableProperty]
    private string _legacyCountText = string.Empty;

    public OptimizeViewModel(OptimizationRegistry optimizationRegistry)
    {
        _optimizationRegistry = optimizationRegistry;
        // Keep subscribed across navigations (both are singletons) so reverts or clears
        // triggered from other pages update the Legacy tab badge immediately.
        _optimizationRegistry.LegacyRevertsChanged += OnLegacyRevertsChanged;
    }

    public ObservableCollection<NavigationViewItem> OptimizationCategories { get; } = [];

    public event Action? OptimizationsLoaded;

    /// <summary>Raised when the conditional Legacy Reverts tab is removed after emptying.</summary>
    public event Action? LegacyTabRemoved;

    protected override async Task InitializeOnceAsync()
    {
        await _optimizationRegistry.EnsurePreloadedAsync().ConfigureAwait(true);
        ReloadCategories();
        OptimizationsLoaded?.Invoke();
    }

    public override async Task OnNavigatedToAsync()
    {
        var wasInitialized = IsInitialized;
        await base.OnNavigatedToAsync();

        if (wasInitialized && _optimizationRegistry.IsPreloaded)
        {
            await _optimizationRegistry.RefreshLegacyRevertsAsync().ConfigureAwait(true);
            SyncLegacyTab();
        }
    }

    private void OnLegacyRevertsChanged()
    {
        void TrySync()
        {
            if (_categoriesLoaded || _legacyNavItem is not null)
                SyncLegacyTab();
        }

        var dispatcher = Application.Current?.Dispatcher ?? _dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            TrySync();
            return;
        }

        _ = dispatcher.InvokeAsync(TrySync);
    }

    private void ReloadCategories()
    {
        OptimizationCategories.Clear();
        _legacyNavItem = null;

        foreach (var category in _optimizationRegistry.OptimizationCategories)
        {
            var item = new NavigationViewItem
            {
                TargetPageType = category
                    .GetType()
                    .GetCustomAttribute<OptimizationCategoryAttribute>()!
                    .PageType,
            };

            // Bind Content instead of assigning it: categories are LocalizedObjects,
            // so the label re-resolves automatically on culture change.
            item.SetBinding(
                ContentControl.ContentProperty,
                new Binding(nameof(IOptimizationCategory.Name))
                {
                    Source = category,
                    Mode = BindingMode.OneWay,
                }
            );

            OptimizationCategories.Add(item);
        }

        _categoriesLoaded = true;
        SyncLegacyTab();
    }

    internal void SyncLegacyTab()
    {
        var count = _optimizationRegistry.LegacyRevertItems.Count;
        LegacyCountText = count.ToString();

        if (count > 0)
        {
            if (_legacyNavItem is null)
            {
                var badge = new InfoBadge { Severity = InfoBadgeSeverity.Caution };
                badge.SetBinding(
                    InfoBadge.ValueProperty,
                    new Binding(nameof(LegacyCountText))
                    {
                        Source = this,
                        Mode = BindingMode.OneWay,
                    }
                );
                _legacyNavItem = new NavigationViewItem
                {
                    TargetPageType = typeof(LegacyRevertsPage),
                    InfoBadge = badge,
                };
                _legacyNavItem.SetBinding(
                    ContentControl.ContentProperty,
                    new Binding("[Optimizer.Legacy.Tab]")
                    {
                        Source = Loc.Instance,
                        Mode = BindingMode.OneWay,
                    }
                );
                OptimizationCategories.Add(_legacyNavItem);
            }

            return;
        }

        if (_legacyNavItem is not null)
        {
            OptimizationCategories.Remove(_legacyNavItem);
            _legacyNavItem = null;
            LegacyTabRemoved?.Invoke();
        }
    }
}
