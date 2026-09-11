using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Configuration;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.Customize;
using optimizerDuck.Services.Optimization;
using optimizerDuck.Services.Optimization.Providers;
using optimizerDuck.Services.Revert;
using optimizerDuck.Services.System;
using optimizerDuck.Services.UI;
using optimizerDuck.UI.Pages;
using optimizerDuck.UI.ViewModels.Dialogs;
using optimizerDuck.UI.ViewModels.Pages;
using optimizerDuck.UI.ViewModels.Windows;
using optimizerDuck.UI.Windows;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;

namespace optimizerDuck;

/// <summary>
///     Application DI registrations, separated from the hosting bootstrap in
///     <see cref="App"/> so the whole graph can be built in a test.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the WPF shell, pages, view models, and application services.
    /// </summary>
    /// <remarks>
    ///     Every service here is a singleton: the app has a single window and a single
    ///     settings file. Per-operation state (the <c>ChangeSet</c>) is created by
    ///     <c>OptimizationService</c> for each apply, not by DI.
    /// </remarks>
    public static IServiceCollection AddOptimizerApplication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<AppSettings>(configuration);

        // Registered explicitly so the graph is buildable without the host builder, which
        // otherwise supplies IConfiguration implicitly.
        services.AddSingleton<IConfiguration>(configuration);

        // WPF UI shell
        services.AddNavigationViewPageProvider();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IContentDialogService, ContentDialogService>();
        services.AddSingleton<ISnackbarService, SnackbarService>();

        // Windows
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();

        // Pages
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<DashboardPage>();
        services.AddSingleton<OptimizeViewModel>();
        services.AddSingleton<OptimizePage>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<SettingsPage>();
        services.AddSingleton<BloatwareViewModel>();
        services.AddSingleton<BloatwarePage>();
        services.AddSingleton<DiskCleanupViewModel>();
        services.AddSingleton<DiskCleanupPage>();
        services.AddSingleton<StartupManagerViewModel>();
        services.AddSingleton<StartupManagerPage>();
        services.AddSingleton<ScheduledTasksViewModel>();
        services.AddSingleton<ScheduledTasksPage>();

        // Dialogs
        services.AddTransient<LegalDialogViewModel>();
        services.AddTransient<optimizerDuck.UI.Dialogs.LegalDialog>();

        // Customize
        services.AddSingleton<CustomizeViewModel>();
        services.AddSingleton<CustomizePage>();
        services.AddAllCustomizeCategoryPages();

        // Optimizations
        services.AddAllOptimizationPages();

        // Managers
        services.AddSingleton<ConfigManager>();
        services.AddSingleton<RevertManager>();
        services.AddSingleton(TimeProvider.System);

        // Services
        services.AddSingleton<ProcessRunner>();
        services.AddSingleton<ShellService>();
        services.AddSingleton<OptimizationRegistry>();
        services.AddSingleton<CustomizeRegistry>();
        services.AddSingleton<OptimizationService>();
        services.AddSingleton<BloatwareService>();
        services.AddSingleton<DiskCleanupService>();
        services.AddSingleton<StartupManagerService>();
        services.AddSingleton<SystemInfoService>();
        services.AddSingleton<StreamService>();
        services.AddSingleton<UpdaterService>();
        services.AddSingleton<IRegistryWatcher, RegistryWatcher>();

        return services;
    }
}
