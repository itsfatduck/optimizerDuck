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

    /// <summary>Gets the registered customize categories. Each category contains its child customize settings. Populated after calling <see cref="RegisterCategories"/> or via preloading.</summary>
    public ICustomizeCategory[] Categories { get; private set; } = [];

    /// <summary>Gets a value that indicates whether the customize categories have been fully discovered.</summary>
    public bool IsPreloaded { get; private set; }

    /// <summary>
    ///     Ensures categories have been discovered before the customize UI binds.
    ///     If preloading has already completed, returns a completed task.
    /// </summary>
    public Task EnsurePreloadedAsync()
    {
        if (IsPreloaded)
            return Task.CompletedTask;
        return PreloadCategoriesAsync();
    }

    /// <summary>Discovers all customize setting categories and their child settings via reflection, then populates <see cref="Categories"/>.</summary>
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

    /// <summary>Builds the navigation items for the UI from the registered categories.</summary>
    /// <returns>A sequence of <see cref="NavigationViewItem"/> instances mapped to customize pages.</returns>
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
