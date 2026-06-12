using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Services;
using optimizerDuck.UI.ViewModels.Pages;

namespace optimizerDuck.Common.Extensions;

public static class CustomizePageRegistryExtensions
{
    public static void AddAllCustomizeCategoryPages(this IServiceCollection services)
    {
        var categoryTypes =
            ReflectionHelper.FindImplementationsInLoadedAssemblies<ICustomizeCategory>();

        foreach (var categoryType in categoryTypes)
        {
            var attr = categoryType.GetCustomAttribute<CustomizeCategoryAttribute>();
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
        Type pageType
    )
    {
        var registry = serviceProvider.GetRequiredService<CustomizeRegistry>();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var registryWatcher = serviceProvider.GetRequiredService<IRegistryWatcher>();
        var category = registry.Categories.First(c => c.GetType() == categoryType);

        var viewModel = new CustomizeCategoryViewModel(category, loggerFactory, registryWatcher);

        return Activator.CreateInstance(pageType, viewModel)!;
    }
}
