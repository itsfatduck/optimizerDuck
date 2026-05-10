using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Configuration;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services;
using optimizerDuck.Services.Managers;
using optimizerDuck.Services.OptimizationServices;
using optimizerDuck.UI.Pages;
using optimizerDuck.UI.ViewModels.Pages;
using optimizerDuck.UI.ViewModels.Windows;
using optimizerDuck.UI.Windows;
using Serilog;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Parsing;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.DependencyInjection;
using Wpf.Ui.Extensions;

namespace optimizerDuck;

public class ScopeBlockTextFormatter : ITextFormatter
{
    public void Format(LogEvent logEvent, TextWriter output)
    {
        // build prefix
        var timestamp = $"{logEvent.Timestamp:yyyy-MM-dd HH:mm:ss}";
        var ctx = logEvent.Properties.TryGetValue("SourceContext", out var sourceContext)
            ? sourceContext is ScalarValue { Value: string s }
                ? s.Split(".")[^1]
                : sourceContext.ToString().Split(".")[^1]
            : "";
        var levelText = logEvent.Level switch
        {
            LogEventLevel.Verbose => "VERBOSE",
            LogEventLevel.Debug => "DEBUG",
            LogEventLevel.Information => "INFO",
            LogEventLevel.Warning => "WARNING",
            LogEventLevel.Error => "ERROR",
            LogEventLevel.Fatal => "FATAL",
            _ => "UNKNOWN",
        };

        //var prefix = $"{timestamp} | {ctx,-67} | {levelText,-7} | "; // byebye 67 char SourceContext truncation, we have a new design now...
        var prefix = $"{timestamp} | {ctx, -35} | {levelText, -7} | ";

        // print message
        output.WriteLine(prefix + RenderWithoutQuotes(logEvent));

        // print property
        foreach (var kvp in logEvent.Properties)
        {
            if (kvp.Key == "SourceContext")
                continue;

            var usedInMessage = logEvent
                .MessageTemplate.Tokens.OfType<PropertyToken>()
                .Any(t => t.PropertyName == kvp.Key);
            if (usedInMessage)
                continue;

            var valueText = kvp.Value is ScalarValue sv
                ? sv.Value?.ToString() ?? ""
                : kvp.Value.ToString();

            output.WriteLine(new string(' ', prefix.Length) + $"{kvp.Key} = {valueText}");
        }

        if (logEvent.Exception != null)
        {
            output.WriteLine("Exception : ");
            output.WriteLine($"    {logEvent.Exception}");
        }
    }

    private static string RenderWithoutQuotes(LogEvent logEvent)
    {
        var output = new StringWriter();
        foreach (var token in logEvent.MessageTemplate.Tokens)
            if (
                token is PropertyToken pt
                && logEvent.Properties.TryGetValue(pt.PropertyName, out var value)
            )
            {
                if (value is ScalarValue { Value: string s })
                    // write string without quotes
                    output.Write(s);
                else
                    // use default rendering for other types
                    pt.Render(logEvent.Properties, output);
            }
            else
            {
                token.Render(logEvent.Properties, output);
            }

        return output.ToString();
    }
}

public partial class App : Application
{
    private IHost? _host;
    private ILogger<App> _logger = null!;
    private bool _allowClose;
    private IContentDialogService? _contentDialogService = null;

    public bool HasPendingChanges { get; set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            await OnStartupAsync(e);
        }
        catch (Exception ex)
        {
            try
            {
                Log.Logger?.Fatal(ex, "Fatal error during startup");
                await Log.CloseAndFlushAsync();
            }
            catch
            {
                // ignore logging failures during fatal startup handling
            }

            System.Windows.MessageBox.Show(
                $"Failed to start optimizerDuck.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "optimizerDuck",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            Shutdown(-1);
        }
    }

    private async Task OnStartupAsync(StartupEventArgs e)
    {
        // Create the required directories if they don't exist
        Directory.CreateDirectory(Shared.ResourcesDirectory);
        Directory.CreateDirectory(Shared.RootDirectory);
        Directory.CreateDirectory(Shared.RevertDirectory);

        var logPath = Path.Combine(Shared.RootDirectory, "optimizerDuck.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                new ScopeBlockTextFormatter(),
                logPath,
                rollingInterval: RollingInterval.Infinite
            )
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureAppConfiguration(c =>
            {
                ConfigManager.ValidateConfig();
                c.AddJsonFile(Path.Combine(Shared.RootDirectory, "appsettings.json"), false, true);
            })
            .ConfigureServices(
                (context, services) =>
                {
                    services.Configure<AppSettings>(context.Configuration);

                    // WPF UI shi
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

                    // Toggle Features
                    services.AddSingleton<FeaturesViewModel>();
                    services.AddSingleton<FeaturesPage>();

                    services.AddAllFeaturesCategoryPages();

                    // Optimizations
                    services.AddAllOptimizationPages();

                    // Managers
                    services.AddSingleton<ConfigManager>();
                    services.AddSingleton<RevertManager>();

                    // Services
                    services.AddSingleton<OptimizationRegistry>();
                    services.AddSingleton<FeatureRegistry>();
                    services.AddSingleton<OptimizationService>();
                    services.AddSingleton<BloatwareService>();
                    services.AddSingleton<DiskCleanupService>();
                    services.AddSingleton<StartupManagerService>();
                    services.AddSingleton<SystemInfoService>();
                    services.AddSingleton<StreamService>();
                    services.AddSingleton<UpdaterService>();
                }
            )
            .Build();

        await _host.StartAsync().ConfigureAwait(false);

        var config = _host.Services.GetRequiredService<ConfigManager>();
        await config.InitializeAsync().ConfigureAwait(false);
        await config.EnsureDefaultsAsync().ConfigureAwait(false);

        var appOptionsMonitor = _host.Services.GetRequiredService<IOptionsMonitor<AppSettings>>();

        // init shell service with config
        ShellService.Init(appOptionsMonitor);

        // init WMI helper for cleanup on abnormal termination
        WmiHelper.Initialize();

        var appSettings = appOptionsMonitor.CurrentValue;

        Loc.Instance.ChangeCulture(new CultureInfo(appSettings.App.Language));

        _logger = _host.Services.GetRequiredService<ILogger<App>>();
        _logger.LogInformation(
            "\n{Logo}\nVersion: {Version}\n\n",
            Shared.RawLogo,
            Shared.FileVersion
        );
        _logger.LogInformation("Loaded language: {Language}", appSettings.App.Language);

        var optimizationRegistry = _host.Services.GetRequiredService<OptimizationRegistry>();
        _logger.LogInformation("Preloading optimizations...");
        await optimizationRegistry.PreloadOptimizations().ConfigureAwait(false);

        // Apply custom accent colors and theme on UI thread
        await Dispatcher.InvokeAsync(() =>
        {
            ApplicationAccentColorManager.Apply(
                Color.FromRgb(254, 209, 20),
                Color.FromRgb(242, 124, 20),
                Color.FromRgb(254, 209, 20),
                Color.FromRgb(242, 124, 20)
            );

            ApplicationThemeManager.Apply(
                appSettings.App.Theme switch
                {
                    ApplicationTheme.Dark => ApplicationTheme.Dark,
                    ApplicationTheme.HighContrast => ApplicationTheme.HighContrast,
                    _ => ApplicationTheme.Light,
                },
                updateAccent: false
            );

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Closing += MainWindow_Closing;
            mainWindow.Show();
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_host != null)
            {
                _host.StopAsync().GetAwaiter().GetResult();
                _host.Dispose();
            }

            // Dispose WMI scopes to prevent resource leaks
            WmiHelper.DisposeScopes();

            Log.CloseAndFlush();
        }
        catch
        {
            // Silently ignore exit errors to prevent blocking shutdown
        }

        base.OnExit(e);
    }

    protected async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        if (!HasPendingChanges)
        {
            return;
        }

        e.Cancel = true;

        try
        {
            _contentDialogService ??= _host!.Services.GetRequiredService<IContentDialogService>();

            var result = await _contentDialogService.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions
                {
                    Title = Translations.Dialog_PendingChanges_Title,
                    Content = Translations.Dialog_PendingChanges_Content,
                    CloseButtonText = Translations.Dialog_PendingChanges_CloseButton,
                    PrimaryButtonText = Translations.Dialog_PendingChanges_PrimaryButton,
                    SecondaryButtonText = Translations.Dialog_PendingChanges_SecondaryButton,
                }
            );

            switch (result)
            {
                case ContentDialogResult.Primary:
                    _logger.LogInformation("User chose to restart PC.");

                    ShellService.CMD("shutdown /r /t 0");
                    break;

                case ContentDialogResult.Secondary:
                    _logger.LogInformation("User chose to restart Explorer.");

                    ShellService.CMD("taskkill /f /im explorer.exe && start explorer.exe");

                    _allowClose = true;
                    Current.Shutdown();
                    break;

                case ContentDialogResult.None:
                    _logger.LogInformation("User chose to exit without applying changes.");

                    _allowClose = true;
                    Current.Shutdown();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show pending changes dialog.");

            _allowClose = true;
            Current.Shutdown();
        }
    }
}
