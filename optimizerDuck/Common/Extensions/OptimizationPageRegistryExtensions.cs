using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Configuration;
using optimizerDuck.Services;
using optimizerDuck.Services.Managers;
using Wpf.Ui;
using OptimizationCategoryViewModel = optimizerDuck.UI.ViewModels.Optimizer.OptimizationCategoryViewModel;

namespace optimizerDuck.Common.Extensions;

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

            services.AddSingleton(
                pageType,
                sp => CreateOptimizationPage(sp, categoryType, pageType)
            );
        }
    }

    private static object CreateOptimizationPage(
        IServiceProvider serviceProvider,
        Type categoryType,
        Type pageType
    )
    {
        var optimizationRegistry = serviceProvider.GetRequiredService<OptimizationRegistry>();
        var optimizationService = serviceProvider.GetRequiredService<OptimizationService>();
        var revertManager = serviceProvider.GetRequiredService<RevertManager>();
        var snackbarService = serviceProvider.GetRequiredService<ISnackbarService>();
        var contentDialogService = serviceProvider.GetRequiredService<IContentDialogService>();
        var logger = serviceProvider.GetRequiredService<ILogger<OptimizationCategoryViewModel>>();
        var appOptionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<AppSettings>>();

        var optimizationCategoryType = optimizationRegistry.GetCategory(categoryType);

        var viewModel = new OptimizationCategoryViewModel(
            optimizationCategoryType,
            optimizationService,
            revertManager,
            snackbarService,
            contentDialogService,
            logger,
            appOptionsMonitor
        );

        return Activator.CreateInstance(pageType, viewModel)!;
    }
}
