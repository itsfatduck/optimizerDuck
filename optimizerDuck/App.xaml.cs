using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Models.Config;
using optimizerDuck.Services;
using optimizerDuck.Services.Managers;
using optimizerDuck.Services.OptimizationServices;
using optimizerDuck.UI.ViewModels.Pages;
using Serilog;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Parsing;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.DependencyInjection;
using DashboardPage = optimizerDuck.UI.Views.Pages.DashboardPage;
using DashboardViewModel = optimizerDuck.UI.ViewModels.Pages.DashboardViewModel;
using MainWindow = optimizerDuck.UI.Views.Windows.MainWindow;
using OptimizePage = optimizerDuck.UI.Views.Pages.OptimizePage;
using SettingsPage = optimizerDuck.UI.Views.Pages.SettingsPage;
using SettingsViewModel = optimizerDuck.UI.ViewModels.Pages.SettingsViewModel;

namespace optimizerDuck;

public class ScopeBlockTextFormatter : ITextFormatter
{
    public void Format(LogEvent logEvent, TextWriter output)
    {
        // build prefix
        var timestamp = $"{logEvent.Timestamp:yyyy-MM-dd HH:mm:ss}";
        var ctx = logEvent.Properties.TryGetValue("SourceContext", out var sourceContext)
            ? sourceContext is ScalarValue { Value: string s }
                ? s.Split(".", 2)[1]
                : sourceContext.ToString().Split(".", 2)[1]
            : "";
        var levelText = logEvent.Level switch
        {
            LogEventLevel.Verbose => "VERBOSE",
            LogEventLevel.Debug => "DEBUG",
            LogEventLevel.Information => "INFO",
            LogEventLevel.Warning => "WARNING",
            LogEventLevel.Error => "ERROR",
            LogEventLevel.Fatal => "FATAL",
            _ => "UNKNOWN"
        };

        var prefix = $"{timestamp} | {ctx,-67} | {levelText,-7} | ";

        // print message
        output.WriteLine(prefix + RenderWithoutQuotes(logEvent));

        // print property
        foreach (var kvp in logEvent.Properties)
        {
            if (kvp.Key == "SourceContext") continue;

            var usedInMessage = logEvent.MessageTemplate.Tokens
                .OfType<PropertyToken>()
                .Any(t => t.PropertyName == kvp.Key);
            if (usedInMessage) continue;

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
            if (token is PropertyToken pt && logEvent.Properties.TryGetValue(pt.PropertyName, out var value))
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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
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
                .ConfigureAppConfiguration((c) =>
                {
                    ConfigManager.ValidateConfig();
                    c.AddJsonFile(Path.Combine(Shared.RootDirectory, "appsettings.json"), false, true);
                })
                .ConfigureServices((context, services) =>
                {
                    services.Configure<AppSettings>(context.Configuration);

                    // WPF UI shi
                    services.AddNavigationViewPageProvider();
                    services.AddSingleton<INavigationService, NavigationService>();
                    services.AddSingleton<IContentDialogService, ContentDialogService>();
                    services.AddSingleton<ISnackbarService, SnackbarService>();

                    // Windows
                    services.AddSingleton<MainWindow>();

                    // Pages
                    services.AddSingleton<DashboardViewModel>();
                    services.AddSingleton<DashboardPage>();

                    services.AddSingleton<OptimizeViewModel>();
                    services.AddSingleton<OptimizePage>();

                    services.AddSingleton<SettingsViewModel>();
                    services.AddSingleton<SettingsPage>();

                    // Optimizations
                    services.AddAllOptimizationPages();

                    // Managers
                    services.AddSingleton<ConfigManager>();
                    services.AddSingleton<RevertManager>();

                    // Services
                    services.AddSingleton<OptimizationRegistry>();
                    services.AddSingleton<OptimizationService>();
                    services.AddSingleton<SystemInfoService>();
                    services.AddSingleton<StreamService>();
                })
                .Build();

            await _host.StartAsync();

            var config = _host.Services.GetRequiredService<ConfigManager>();
            await config.InitializeAsync();

            var appOptionsMonitor = _host.Services.GetRequiredService<IOptionsMonitor<AppSettings>>();
            // init shell service with config
            ShellService.Init(appOptionsMonitor);

            var appSettings = appOptionsMonitor.CurrentValue;

            Loc.Instance.ChangeCulture(new CultureInfo(appSettings.App.Language));

            _logger = _host.Services.GetRequiredService<ILogger<App>>();
            _logger.LogInformation("\n{Logo}\nVersion: {Version}\n\n", Shared.RawLogo, Shared.FileVersion);
            _logger.LogInformation("Loaded language: {Language}", appSettings.App.Language);

            var optimizationRegistry = _host.Services.GetRequiredService<OptimizationRegistry>();
            _logger.LogInformation("Preloading optimizations...");
            await optimizationRegistry.PreloadOptimizations();

            // Apply custom accent colors
            ApplicationAccentColorManager.Apply(
                Color.FromRgb(254, 209, 20),
                Color.FromRgb(242, 124, 20),
                Color.FromRgb(254, 209, 20),
                Color.FromRgb(242, 124, 20)
            );

            ApplicationThemeManager.Apply(
                appSettings.App.Theme switch
                {
                    "Dark" => ApplicationTheme.Dark,
                    "HighContrast" => ApplicationTheme.HighContrast,
                    _ => ApplicationTheme.Light
                },
                updateAccent: false
            );

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
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

            MessageBox.Show(
                $"Failed to start optimizerDuck.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "optimizerDuck",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        await Log.CloseAndFlushAsync();

        base.OnExit(e);
    }
}
