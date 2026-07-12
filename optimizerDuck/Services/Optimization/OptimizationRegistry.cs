using System.Collections.ObjectModel;
using System.Reflection;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
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
    ///     If preloading has already completed, returns a completed task.
    /// </summary>
    public Task EnsurePreloadedAsync()
    {
        if (IsPreloaded)
            return Task.CompletedTask;
        return PreloadOptimizationsAsync();
    }

    /// <summary>Discovers all optimization categories and their optimizations via reflection, then loads the applied state from revert data on disk.</summary>
    public async Task PreloadOptimizationsAsync()
    {
        // Run reflection work on background thread to avoid blocking startup
        var optimizationCategories = await Task.Run(() =>
                ReflectionHelper
                    .FindImplementationsInLoadedAssemblies<IOptimizationCategory>()
                    .Select(t =>
                    {
                        var optimizations = new ObservableCollection<IOptimization>(
                            t.GetNestedTypes(BindingFlags.Public)
                                .Where(nt => typeof(IOptimization).IsAssignableFrom(nt))
                                .Select(nt =>
                                {
                                    var opt = (IOptimization)Activator.CreateInstance(nt)!;

                                    if (opt is BaseOptimization bo)
                                        bo.OwnerType = t;

                                    return opt;
                                })
                                .ToList()
                        );

                        if (optimizations.Count == 0)
                            return null;

                        var instance = (IOptimizationCategory)Activator.CreateInstance(t)!;

                        var optProp = t.GetProperty(
                            nameof(IOptimizationCategory.Optimizations),
                            BindingFlags.Public | BindingFlags.Instance
                        );
                        if (optProp != null && optProp.CanWrite)
                            optProp.SetValue(instance, optimizations);

                        return instance;
                    })
                    .Where(c => c != null) // skip nulls
                    .Cast<IOptimizationCategory>()
                    .OrderBy(c => c.Order)
                    .ToArray()
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
