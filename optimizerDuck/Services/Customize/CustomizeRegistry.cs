using System.Collections.ObjectModel;
using System.Reflection;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Conditions;
using optimizerDuck.Domain.Customize.Models;
using Wpf.Ui.Controls;

namespace optimizerDuck.Services.Customize;

public class CustomizeRegistry
{
    private readonly ILogger<CustomizeRegistry> _logger;

    public CustomizeRegistry(ILogger<CustomizeRegistry> logger)
    {
        _logger = logger;
    }

    /// <summary>Gets the customize categories discovered by preloading.</summary>
    public ICustomizeCategory[] Categories { get; private set; } = [];

    /// <summary>
    ///     Gets a value that indicates whether the customize categories have been fully
    ///     discovered.
    /// </summary>
    public bool IsPreloaded { get; private set; }

    /// <summary>
    ///     Ensures categories have been discovered before the customize UI binds.
    ///     Concurrent callers share a single discovery; a failed discovery is retried.
    /// </summary>
    public async Task EnsurePreloadedAsync()
    {
        if (IsPreloaded)
            return;

        _preloadTask ??= PreloadCategoriesAsync();
        try
        {
            await _preloadTask.ConfigureAwait(false);
        }
        finally
        {
            if (!IsPreloaded)
                _preloadTask = null;
        }
    }

    private Task? _preloadTask;

    /// <summary>Discovers the customize categories and their settings by reflection.</summary>
    public async Task PreloadCategoriesAsync()
    {
        // Run reflection work on background thread to avoid blocking startup
        var categories = await Task.Run(() =>
            {
                _logger.LogInformation("Discovering customize setting categories...");

                var result = CategoryDiscovery.Discover<ICustomizeCategory, ICustomizeSetting>(
                    nameof(ICustomizeCategory.Features),
                    t =>
                        CategoryDiscovery.NestedItems<ICustomizeSetting>(
                            t,
                            (opt, owner) =>
                            {
                                if (opt is BaseCustomizeSetting bo)
                                {
                                    bo.OwnerType = owner;
                                    ConditionValidation.Validate(bo.ConditionType, bo.FeatureKey);
                                }
                            }
                        ),
                    c => (int)c.Order
                );

                _logger.LogInformation(
                    "Registered {CategoryCount} customize categories with {SettingCount} total settings",
                    result.Length,
                    result.Sum(c => c.Features.Count)
                );

                return result;
            })
            .ConfigureAwait(false);

        Categories = categories;
        IsPreloaded = true;
    }

    /// <summary>Builds a navigation item for each registered category.</summary>
    /// <returns>The items, each mapped to its customize page.</returns>
    public IEnumerable<NavigationViewItem> GetNavigationItems()
    {
        if (Categories.Length == 0 && !IsPreloaded)
            _logger.LogWarning(
                "GetNavigationItems() called before categories were preloaded, returning empty. "
                    + "Ensure EnsurePreloadedAsync() was awaited during startup."
            );

        return Categories
            .Select(c => new NavigationViewItem
            {
                Content = c.Name,
                TargetPageType = c.GetType()
                    .GetCustomAttribute<CustomizeCategoryAttribute>()
                    ?.PageType,
                TargetPageTag = c.GetType().Name,
            })
            .Where(item => item.TargetPageType != null);
    }
}
