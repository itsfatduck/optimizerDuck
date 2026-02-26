using System.Collections.ObjectModel;
using System.Reflection;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Optimizers;

namespace optimizerDuck.Services;

public class OptimizationRegistry(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<OptimizationRegistry>();
    public IOptimizationCategory[] OptimizationCategories { get; set; } = [];

    public async Task PreloadOptimizations()
    {
        // Clear state cache before preloading to ensure fresh data
        OptimizationService.ClearStateCache();

        var optimizationCategories = ReflectionHelper
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

                var optProp = t.GetProperty(nameof(IOptimizationCategory.Optimizations),
                    BindingFlags.Public | BindingFlags.Instance);
                if (optProp != null && optProp.CanWrite)
                    optProp.SetValue(instance, optimizations);

                return instance;
            })
            .Where(c => c != null) // skip nulls
            .Cast<IOptimizationCategory>()
            .OrderBy(c => c.Order)
            .ToArray();

        _logger.LogInformation("Total {CategoryCount} categories and {OptimizationCount} optimizations found",
            optimizationCategories.Length, optimizationCategories.Sum(c => c.Optimizations.Count));

        await OptimizationService.UpdateOptimizationStateAsync(optimizationCategories.SelectMany(c => c.Optimizations));


        OptimizationCategories = optimizationCategories;
    }

    public IOptimizationCategory GetCategory(Type type)
    {
        return OptimizationCategories.First(c => c.GetType() == type);
    }
}