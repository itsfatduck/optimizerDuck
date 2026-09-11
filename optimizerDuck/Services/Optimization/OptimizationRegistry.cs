using System.Collections.ObjectModel;
using System.Reflection;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Conditions;
using optimizerDuck.Domain.Optimizations.Models;

namespace optimizerDuck.Services.Optimization;

public class OptimizationRegistry(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<OptimizationRegistry>();

    /// <summary>Gets or sets the discovered optimization categories after preloading. Each category contains its child optimizations.</summary>
    public IOptimizationCategory[] OptimizationCategories { get; set; } = [];

    /// <summary>Gets a value that indicates whether the optimizations have been fully discovered and their applied states loaded.</summary>
    public bool IsPreloaded { get; private set; }

    /// <summary>
    ///     Ensures categories and applied-state are loaded before the optimize UI binds.
    ///     Concurrent callers share a single discovery; a failed discovery is retried.
    /// </summary>
    public async Task EnsurePreloadedAsync()
    {
        if (IsPreloaded)
            return;

        _preloadTask ??= PreloadOptimizationsAsync();
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

    /// <summary>Discovers all optimization categories and their optimizations via reflection, then loads the applied state from revert data on disk.</summary>
    public async Task PreloadOptimizationsAsync()
    {
        var seenIds = new HashSet<Guid>();
        // Run reflection work on background thread to avoid blocking startup
        var optimizationCategories = await Task.Run(() =>
                CategoryDiscovery.Discover<IOptimizationCategory, IOptimization>(
                    nameof(IOptimizationCategory.Optimizations),
                    t =>
                        CategoryDiscovery.NestedItems<IOptimization>(
                            t,
                            (opt, owner) =>
                            {
                                if (opt is BaseOptimization bo)
                                {
                                    bo.OwnerType = owner;
                                    OptimizationValidation.Validate(bo, seenIds);
                                    ConditionValidation.Validate(
                                        bo.ConditionType,
                                        bo.OptimizationKey
                                    );
                                }
                            }
                        ),
                    c => (int)c.Order
                )
            )
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Total {CategoryCount} categories and {OptimizationCount} optimizations found",
            optimizationCategories.Length,
            optimizationCategories.Sum(c => c.Optimizations.Count)
        );

        await OptimizationService
            .UpdateOptimizationStateAsync(optimizationCategories.SelectMany(c => c.Optimizations))
            .ConfigureAwait(false);

        OptimizationCategories = optimizationCategories;
        IsPreloaded = true;
    }

    /// <summary>Gets a category by its runtime type. Categories must have been preloaded first.</summary>
    /// <param name="type">The runtime type of the category to retrieve.</param>
    /// <returns>The matching <see cref="IOptimizationCategory"/> instance.</returns>
    public IOptimizationCategory GetCategory(Type type)
    {
        return OptimizationCategories.First(c => c.GetType() == type);
    }
}
