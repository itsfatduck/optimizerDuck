using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Configuration;
using optimizerDuck.Services.Customize;
using optimizerDuck.Services.Optimization;
using optimizerDuck.Services.Revert;
using optimizerDuck.Services.System;
using optimizerDuck.UI.ViewModels.Optimizer;
using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui;
using OptimizationCategoryViewModel = optimizerDuck.UI.ViewModels.Optimizer.OptimizationCategoryViewModel;

namespace optimizerDuck.Common.Extensions;

/// <summary>
///     Shared plumbing for the reflection-discovered category pages. Kept in one place so
///     the optimization and customize paths cannot drift apart.
/// </summary>
/// <remarks>
///     A category page needs the <em>discovered category instance</em> (built by the
///     registry during preload, not by the container), so the page is registered against a
///     factory that pulls it from the registry. Everything else in the view model comes
///     from DI.
/// </remarks>
internal static class CategoryPageFactory
{
    /// <summary>Registers one singleton page per discovered category type.</summary>
    public static void AddCategoryPages<TCategory>(
        IServiceCollection services,
        Func<Type, Type?> resolvePageType,
        Func<IServiceProvider, Type, object> createViewModel
    )
        where TCategory : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(resolvePageType);
        ArgumentNullException.ThrowIfNull(createViewModel);

        foreach (
            var categoryType in ReflectionHelper.FindImplementationsInLoadedAssemblies<TCategory>()
        )
        {
            var pageType = resolvePageType(categoryType);
            if (pageType is null)
                continue;

            var capturedCategoryType = categoryType;
            services.AddSingleton(
                pageType,
                sp => Activator.CreateInstance(pageType, createViewModel(sp, capturedCategoryType))!
            );
        }
    }
}

/// <summary>Registers the optimize-category pages discovered on <see cref="IOptimizationCategory"/>.</summary>
public static class OptimizationPageRegistryExtensions
{
    public static void AddAllOptimizationPages(this IServiceCollection services)
    {
        CategoryPageFactory.AddCategoryPages<IOptimizationCategory>(
            services,
            categoryType =>
                categoryType.GetCustomAttribute<OptimizationCategoryAttribute>()?.PageType,
            CreateViewModel
        );
    }

    private static object CreateViewModel(IServiceProvider sp, Type categoryType)
    {
        return new OptimizationCategoryViewModel(
            sp.GetRequiredService<OptimizationRegistry>().GetCategory(categoryType),
            sp.GetRequiredService<OptimizationService>(),
            sp.GetRequiredService<RevertManager>(),
            sp.GetRequiredService<ISnackbarService>(),
            sp.GetRequiredService<IContentDialogService>(),
            sp.GetRequiredService<SystemInfoService>(),
            sp.GetRequiredService<ILogger<OptimizationCategoryViewModel>>(),
            sp.GetRequiredService<IOptionsMonitor<AppSettings>>()
        );
    }
}

/// <summary>Registers the customize-category pages discovered on <see cref="ICustomizeCategory"/>.</summary>
public static class CustomizePageRegistryExtensions
{
    public static void AddAllCustomizeCategoryPages(this IServiceCollection services)
    {
        CategoryPageFactory.AddCategoryPages<ICustomizeCategory>(
            services,
            categoryType => categoryType.GetCustomAttribute<CustomizeCategoryAttribute>()?.PageType,
            CreateViewModel
        );
    }

    private static object CreateViewModel(IServiceProvider sp, Type categoryType)
    {
        var registry = sp.GetRequiredService<CustomizeRegistry>();

        return new CustomizeCategoryViewModel(
            registry.Categories.First(c => c.GetType() == categoryType),
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<IRegistryWatcher>(),
            sp.GetRequiredService<SystemInfoService>()
        );
    }
}
