using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using Wpf.Ui;
using OptimizationCategoryViewModel = optimizerDuck.UI.ViewModels.Optimizer.OptimizationCategoryViewModel;

namespace optimizerDuck.Services;

public static class OptimizationPageRegistryExtensions
{
    public static void AddAllOptimizationPages(this IServiceCollection services)
    {
        var categoryTypes = ReflectionHelper
            .FindImplementationsInLoadedAssemblies<IOptimizationCategory>()
            .ToList();

        foreach (var categoryType in categoryTypes)
        {
            var attr = categoryType.GetCustomAttribute<OptimizationCategoryAttribute>()!;
            var pageType = attr.PageType!;

            services.AddSingleton(pageType, sp => CreateOptimizationPage(sp, categoryType, pageType));
        }
    }

    private static object CreateOptimizationPage(
        IServiceProvider serviceProvider,
        Type categoryType,
        Type pageType)
    {
        var optimizationRegistry = serviceProvider.GetRequiredService<OptimizationRegistry>();
        var optimizationService = serviceProvider.GetRequiredService<OptimizationService>();
        var snackbarService = serviceProvider.GetRequiredService<ISnackbarService>();
        var contentDialogService = serviceProvider.GetRequiredService<IContentDialogService>();
        var logger = serviceProvider.GetRequiredService<ILogger<OptimizationCategoryViewModel>>();

        var optimizationCategoryType = optimizationRegistry.GetCategory(categoryType);

        var viewModel = new OptimizationCategoryViewModel(
            optimizationCategoryType,
            optimizationService,
            snackbarService,
            contentDialogService,
            logger
        );

        return Activator.CreateInstance(pageType, viewModel)!;
    }
}