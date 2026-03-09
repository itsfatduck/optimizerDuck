using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Services;
using optimizerDuck.UI.ViewModels.Pages;

namespace optimizerDuck.Common.Extensions;

public static class FeaturesCategoryPageRegistryExtensions
{
    public static void AddAllFeaturesCategoryPages(this IServiceCollection services)
    {
        var categoryTypes = ReflectionHelper
            .FindImplementationsInLoadedAssemblies<IFeatureCategory>()
            .ToList();

        foreach (var categoryType in categoryTypes)
        {
            var attr = categoryType.GetCustomAttribute<FeatureCategoryAttribute>();
            if (attr?.PageType != null)
            {
                var pageType = attr.PageType;
                services.AddSingleton(pageType, sp => CreatePage(sp, categoryType, pageType));
            }
        }
    }

    private static object CreatePage(
        IServiceProvider serviceProvider,
        Type categoryType,
        Type pageType)
    {
        var registry = serviceProvider.GetRequiredService<FeatureRegistry>();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var category = registry.Categories.First(c => c.GetType() == categoryType);

        var viewModel = new FeatureCategoryViewModel(category, loggerFactory);

        return Activator.CreateInstance(pageType, viewModel)!;
    }
}