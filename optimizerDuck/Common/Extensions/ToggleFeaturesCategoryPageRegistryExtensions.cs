using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Services;
using optimizerDuck.UI.ViewModels.Pages;
using optimizerDuck.UI.ViewModels.ToggleFeatures;
using Wpf.Ui;
using ToggleFeaturesCategoryViewModel = optimizerDuck.UI.ViewModels.ToggleFeatures.ToggleFeaturesCategoryViewModel;

namespace optimizerDuck.Common.Extensions;

public static class ToggleFeaturesCategoryPageRegistryExtensions
{
    public static void AddAllToggleFeatureCategoryPages(this IServiceCollection services)
    {
        var categoryTypes = ReflectionHelper
            .FindImplementationsInLoadedAssemblies<IToggleFeaturesCategory>()
            .ToList();

        foreach (var categoryType in categoryTypes)
        {
            var attr = categoryType.GetCustomAttribute<ToggleFeatureCategoryAttribute>();
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
        var registry = serviceProvider.GetRequiredService<ToggleFeaturesRegistry>();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var category = registry.Categories.First(c => c.GetType() == categoryType);

        var viewModel = new ToggleFeaturesCategoryViewModel(category, loggerFactory);

        return Activator.CreateInstance(pageType, viewModel)!;
    }
}