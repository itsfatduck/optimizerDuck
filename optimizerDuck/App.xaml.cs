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
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.Customize;
using optimizerDuck.Services.Optimization;
using optimizerDuck.Services.Revert;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.Services.UI;
using optimizerDuck.UI.Behaviors;
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
    private const string ContinuationIndent =
        "                                                                      ";

    public void Format(LogEvent logEvent, TextWriter output)
    {
        Span<char> tsBuf = stackalloc char[19];
        if (logEvent.Timestamp.TryFormat(tsBuf, out var tsLen, "yyyy-MM-dd HH:mm:ss"))
            output.Write(tsBuf[..tsLen]);
        else
            output.Write(logEvent.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));

        output.Write(" | ");

        ReadOnlySpan<char> ctxSpan = ReadOnlySpan<char>.Empty;
        if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
        {
            var raw = sourceContext is ScalarValue { Value: string s }
                ? s.AsSpan()
                : sourceContext.ToString().AsSpan();
            var lastDot = raw.LastIndexOf('.');
            ctxSpan = lastDot >= 0 ? raw[(lastDot + 1)..] : raw;
        }

        output.Write(ctxSpan);
        if (ctxSpan.Length < 35)
            output.Write(ContinuationIndent.AsSpan(0, 35 - ctxSpan.Length));

        output.Write(" | ");

        var levelText = logEvent.Level switch
        {
            LogEventLevel.Verbose => "VERBOSE",
            LogEventLevel.Debug => "DEBUG  ",
            LogEventLevel.Information => "INFO   ",
            LogEventLevel.Warning => "WARNING",
            LogEventLevel.Error => "ERROR  ",
            LogEventLevel.Fatal => "FATAL  ",
            _ => "UNKNOWN",
        };
        output.Write(levelText);
        output.Write(" | ");

        RenderWithoutQuotes(logEvent, output);
        output.WriteLine();

        foreach (var kvp in logEvent.Properties)
        {
            if (kvp.Key == "SourceContext")
                continue;

            var usedInMessage = false;
            foreach (var token in logEvent.MessageTemplate.Tokens)
            {
                if (token is PropertyToken pt && pt.PropertyName == kvp.Key)
                {
                    usedInMessage = true;
                    break;
                }
            }

            if (usedInMessage)
                continue;

            output.Write(ContinuationIndent);
            if (ctxSpan.Length > 35)
                output.Write(new string(' ', ctxSpan.Length - 35));
            output.Write(kvp.Key);
            output.Write(" = ");
            if (kvp.Value is ScalarValue sv)
                output.Write(sv.Value?.ToString() ?? string.Empty);
            else
                output.Write(kvp.Value.ToString());
            output.WriteLine();
        }

        if (logEvent.Exception != null)
        {
            output.WriteLine("Exception : ");
            output.Write("    ");
            output.WriteLine(logEvent.Exception);
        }
    }

    private static void RenderWithoutQuotes(LogEvent logEvent, TextWriter output)
    {
        foreach (var token in logEvent.MessageTemplate.Tokens)
        {
            if (
                token is PropertyToken pt
                && logEvent.Properties.TryGetValue(pt.PropertyName, out var value)
            )
            {
                if (value is ScalarValue { Value: string s })
                    output.Write(s);
                else
                    pt.Render(logEvent.Properties, output);
            }
            else
            {
                token.Render(logEvent.Properties, output);
            }
        }
    }
}

public partial class App : Application
{
    private IHost? _host;

    public IHost? AppHost => _host;
    private ILogger<App> _logger = null!;
    private bool _allowClose;
    private IContentDialogService? _contentDialogService = null;

    private bool _hasPendingChanges;

    public bool HasPendingChanges
    {
        get => _hasPendingChanges;
        set
        {
            if (_hasPendingChanges == value)
                return;
            _hasPendingChanges = value;
            Dispatcher.InvokeAsync(() =>
            {
                if (Current.MainWindow is MainWindow mw)
                    mw.UpdatePendingIndicator(value);
            });
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        _ = SafeFireAndForgetAsync(() => OnStartupAsync(e));
    }

    /// <summary>
    ///     Applies the duck-yellow accent palette tuned for the given theme. Light
    ///     uses deeper honey tones so accent text and fills stay readable on white;
    ///     dark keeps the brighter golden tones. Centralized so startup and theme
    ///     switches always use the same palettes.
    /// </summary>
    private readonly record struct AccentPalette(
        Color SystemAccent,
        Color Primary,
        Color Secondary,
        Color Tertiary,
        Color FillDefault,
        Color FillSecondary,
        Color FillTertiary,
        Color TextPrimary,
        Color TextSecondary,
        Color TextTertiary,
        Color OnAccentPrimary,
        Color OnAccentSecondary,
        Color AccentYellow
    );

    private static AccentPalette GetAccentPalette(ApplicationTheme theme)
    {
        if (theme == ApplicationTheme.Dark)
        {
            var primary = Color.FromRgb(235, 193, 94);
            var secondary = Color.FromRgb(255, 247, 200);
            var tertiary = Color.FromRgb(255, 243, 131);
            return new AccentPalette(
                SystemAccent: Color.FromRgb(216, 155, 29),
                Primary: primary,
                Secondary: secondary,
                Tertiary: tertiary,
                FillDefault: secondary,
                FillSecondary: Color.FromArgb(0xE5, 255, 247, 200),
                FillTertiary: Color.FromArgb(0xCC, 255, 247, 200),
                TextPrimary: secondary,
                TextSecondary: tertiary,
                TextTertiary: primary,
                OnAccentPrimary: Colors.Black,
                OnAccentSecondary: Color.FromArgb(0x80, 0, 0, 0),
                AccentYellow: Color.FromRgb(255, 209, 20)
            );
        }

        var lightPrimary = Color.FromRgb(138, 94, 0);
        var lightSecondary = Color.FromRgb(122, 88, 0);
        var lightTertiary = Color.FromRgb(110, 75, 0);
        return new AccentPalette(
            SystemAccent: lightPrimary,
            Primary: lightPrimary,
            Secondary: lightSecondary,
            Tertiary: lightTertiary,
            FillDefault: lightPrimary,
            FillSecondary: Color.FromArgb(0xE5, 138, 94, 0),
            FillTertiary: Color.FromArgb(0xCC, 138, 94, 0),
            TextPrimary: lightSecondary,
            TextSecondary: lightTertiary,
            TextTertiary: lightPrimary,
            OnAccentPrimary: Colors.White,
            OnAccentSecondary: Color.FromArgb(0xB3, 255, 255, 255),
            AccentYellow: lightPrimary
        );
    }

    internal static void ApplyAccentFor(ApplicationTheme theme)
    {
        var palette = GetAccentPalette(theme);
        ApplicationAccentColorManager.Apply(
            systemAccent: palette.SystemAccent,
            primaryAccent: palette.Primary,
            secondaryAccent: palette.Secondary,
            tertiaryAccent: palette.Tertiary
        );
        if (Current is { } app)
            RefreshAccentBrushes(app.Resources, theme);
    }

    internal static void RefreshAccentBrushes(
        ResourceDictionary resources,
        ApplicationTheme theme
    )
    {
        var palette = GetAccentPalette(theme);

        static SolidColorBrush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        var primaryBrush = CreateFrozenBrush(palette.Primary);
        var secondaryBrush = CreateFrozenBrush(palette.Secondary);
        var tertiaryBrush = CreateFrozenBrush(palette.Tertiary);
        var fillDefaultBrush = CreateFrozenBrush(palette.FillDefault);
        var fillSecondaryBrush = CreateFrozenBrush(palette.FillSecondary);
        var fillTertiaryBrush = CreateFrozenBrush(palette.FillTertiary);
        var textPrimaryBrush = CreateFrozenBrush(palette.TextPrimary);
        var textSecondaryBrush = CreateFrozenBrush(palette.TextSecondary);
        var textTertiaryBrush = CreateFrozenBrush(palette.TextTertiary);
        var onAccentPrimaryBrush = CreateFrozenBrush(palette.OnAccentPrimary);
        var onAccentSecondaryBrush = CreateFrozenBrush(palette.OnAccentSecondary);

        resources["SystemAccentColorPrimaryBrush"] = primaryBrush;
        resources["SystemAccentColorSecondaryBrush"] = secondaryBrush;
        resources["SystemAccentColorTertiaryBrush"] = tertiaryBrush;
        resources["AccentFillColorDefaultBrush"] = fillDefaultBrush;
        resources["AccentFillColorSecondaryBrush"] = fillSecondaryBrush;
        resources["AccentFillColorTertiaryBrush"] = fillTertiaryBrush;
        resources["AccentTextFillColorPrimaryBrush"] = textPrimaryBrush;
        resources["AccentTextFillColorSecondaryBrush"] = textSecondaryBrush;
        resources["AccentTextFillColorTertiaryBrush"] = textTertiaryBrush;
        resources["TextOnAccentFillColorPrimaryBrush"] = onAccentPrimaryBrush;
        resources["TextOnAccentFillColorSecondaryBrush"] = onAccentSecondaryBrush;

        resources["AccentButtonBackground"] = fillDefaultBrush;
        resources["AccentButtonBackgroundPointerOver"] = fillSecondaryBrush;
        resources["AccentButtonBackgroundPressed"] = fillTertiaryBrush;
        resources["AccentButtonForeground"] = onAccentPrimaryBrush;
        resources["AccentButtonForegroundPointerOver"] = onAccentPrimaryBrush;
        resources["AccentButtonForegroundPressed"] = onAccentSecondaryBrush;
        resources["HyperlinkButtonForeground"] = textPrimaryBrush;
        resources["HyperlinkButtonForegroundPointerOver"] = textSecondaryBrush;
        resources["HyperlinkButtonForegroundPressed"] = textTertiaryBrush;
        resources["BadgeBackground"] = fillDefaultBrush;
        resources["AccentYellowBrush"] = CreateFrozenBrush(palette.AccentYellow);

        resources["ToggleSwitchFillOn"] = primaryBrush;
        resources["ToggleSwitchFillOnPointerOver"] = fillSecondaryBrush;
        resources["ToggleSwitchFillOnPressed"] = fillTertiaryBrush;
        resources["ToggleSwitchStrokeOn"] = primaryBrush;
        resources["ToggleSwitchStrokeOnPointerOver"] = fillSecondaryBrush;
        resources["ToggleSwitchStrokeOnPressed"] = fillTertiaryBrush;
        resources["ToggleSwitchKnobFillOn"] = onAccentPrimaryBrush;
        resources["ToggleSwitchKnobFillOnPointerOver"] = onAccentPrimaryBrush;
        resources["ToggleSwitchKnobFillOnPressed"] = onAccentPrimaryBrush;
    }

    private static async Task SafeFireAndForgetAsync(Func<Task> taskFactory)
    {
        try
        {
            await taskFactory();
        }
        catch (Exception ex)
        {
            try
            {
                await HandleStartupErrorStaticAsync(ex);
            }
            catch { }
        }
    }

    private static async Task HandleStartupErrorStaticAsync(Exception ex)
    {
        LogExceptionToFile("App.Startup", ex);

        try
        {
            Log.Logger?.Fatal(ex, "Fatal error during startup");
            await Log.CloseAndFlushAsync();
        }
        catch
        {
            // ignore logging failures during fatal startup handling
        }

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            System.Windows.MessageBox.Show(
                $"Failed to start optimizerDuck.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "optimizerDuck",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            Application.Current.Shutdown(-1);
        });
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString());
        LogExceptionToFile("AppDomain.UnhandledException", ex);
        Log.Logger?.Fatal(
            ex,
            "Unhandled AppDomain exception. IsTerminating={IsTerminating}",
            e.IsTerminating
        );
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogExceptionToFile("UnobservedTaskException", e.Exception);
        Log.Logger?.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }

    private void OnDispatcherUnhandledException(
        object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e
    )
    {
        LogExceptionToFile("DispatcherUnhandledException", e.Exception);
        Log.Logger?.Error(e.Exception, "Unhandled UI dispatcher exception");

        // The exception is swallowed below, so the user is told about it here.
        try
        {
            _host?.Services.GetService<UserErrorSurface>()?.TryReport(e.Exception);
        }
        catch (Exception ex)
        {
            Log.Logger?.Warning(ex, "Could not report the unhandled exception to the user");
        }

        e.Handled = true;
    }

    private static void LogExceptionToFile(string category, Exception ex)
    {
        try
        {
            var crashDir = Path.Combine(Shared.RootDirectory, "Crashes");
            Directory.CreateDirectory(crashDir);
            var crashFile = Path.Combine(
                crashDir,
                $"crash_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.log"
            );
            File.WriteAllText(
                crashFile,
                $"""
                [{category}] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}
                Type: {ex.GetType().FullName}
                Message: {ex.Message}
                StackTrace:
                {ex.StackTrace}

                {(ex.InnerException is not null ? $"Inner: {ex.InnerException}" : "")}
                """
            );
        }
        catch
        {
            // cannot do anything if crash logging itself fails
        }
    }

    private async Task OnStartupAsync(StartupEventArgs e)
    {
        Directory.CreateDirectory(Shared.RootDirectory);
        Directory.CreateDirectory(Shared.ResourcesDirectory);
        Directory.CreateDirectory(Shared.DownloadsDirectory);
        Directory.CreateDirectory(Shared.AssetsDirectory);
        Directory.CreateDirectory(Shared.RevertDirectory);
        Directory.CreateDirectory(Shared.HistoryDirectory);

        var logPath = Path.Combine(Shared.RootDirectory, "optimizerDuck.log");
        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Verbose()
#else
            .MinimumLevel.Information()
#endif
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                new ScopeBlockTextFormatter(),
                logPath,
                rollingInterval: RollingInterval.Infinite,
                buffered: true,
                flushToDiskInterval: TimeSpan.FromSeconds(2),
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 3
            )
            .CreateLogger();

        using var bootstrapLoggerFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory(
            Log.Logger,
            dispose: false
        );
        var initialConfigCache = ConfigManager.ValidateConfigAndReturnCache(
            bootstrapLoggerFactory.CreateLogger(typeof(ConfigManager))
        );

        var builder = Microsoft.Extensions.Hosting.Host.CreateEmptyApplicationBuilder(
            new HostApplicationBuilderSettings { DisableDefaults = true }
        );
        builder.Configuration.AddJsonFile(
            Path.Combine(Shared.RootDirectory, "appsettings.json"),
            optional: false,
            reloadOnChange: false
        );
        // Fail fast instead of discovering a broken graph later: ValidateOnBuild catches
        // an unbuildable registration, ValidateScopes catches a scoped service leaking
        // into a singleton (the ChangeSet must never be resolved from the root provider).
        builder.ConfigureContainer(
            new DefaultServiceProviderFactory(
                new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }
            )
        );
        builder.Services.AddSerilog(Log.Logger, dispose: false);
        builder.Services.AddOptimizerApplication(builder.Configuration);

        _host = builder.Build();

        await _host.StartAsync();

        var config = _host.Services.GetRequiredService<ConfigManager>();
        if (initialConfigCache is not null)
            config.InitializeFromCache(initialConfigCache);
        else
            await config.InitializeAsync();
        await config.EnsureDefaultsAsync();

        var appOptionsMonitor = _host.Services.GetRequiredService<IOptionsMonitor<AppSettings>>();

        // init WMI helper for cleanup on abnormal termination
        WmiHelper.Initialize();

        var appSettings = appOptionsMonitor.CurrentValue;

        SmoothScrollBehavior.GlobalEnabled = appSettings.Optimize.SmoothScrolling;

        await Dispatcher.InvokeAsync(() =>
        {
            Loc.Instance.ChangeCulture(new CultureInfo(appSettings.App.Language));
        });

        _logger = _host.Services.GetRequiredService<ILogger<App>>();
        _logger.LogInformation(
            "\n{Logo}\nVersion: {Version}\n\n",
            Shared.RawLogo,
            Shared.FileVersion
        );
        _logger.LogInformation("Loaded language: {Language}", appSettings.App.Language);

        var optimizationRegistry = _host.Services.GetRequiredService<OptimizationRegistry>();

        await Dispatcher.InvokeAsync(() =>
        {
            // Theme first: ApplyAccentFor reads GetAppTheme() cache to pick
            // AccentFillColorDefault (secondary for Dark, primary for Light).
            ApplicationThemeManager.Apply(
                appSettings.App.Theme switch
                {
                    ApplicationTheme.Dark => ApplicationTheme.Dark,
                    ApplicationTheme.HighContrast => ApplicationTheme.HighContrast,
                    _ => ApplicationTheme.Light,
                },
                updateAccent: false
            );
            ApplyAccentFor(appSettings.App.Theme);

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Closing += MainWindow_Closing;
            mainWindow.Show();
        });

        _logger.LogInformation("Preloading optimizations and customize settings...");
        var customizeRegistry = _host.Services.GetRequiredService<CustomizeRegistry>();
        await Task.WhenAll(
                optimizationRegistry.PreloadOptimizationsAsync(),
                customizeRegistry.PreloadCategoriesAsync()
            )
            .ConfigureAwait(false);

        RevertManager.RemoveOrphanedTempFiles(_logger);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_host != null)
            {
                try
                {
                    _host.StopAsync().Wait(TimeSpan.FromSeconds(2));
                }
                catch (Exception ex)
                {
                    LogExceptionToFile("App.OnExit.StopAsync", ex);
                }
                _host.Dispose();
            }

            WmiHelper.DisposeScopes();
            Log.CloseAndFlush();
        }
        catch (Exception ex)
        {
            LogExceptionToFile("App.OnExit", ex);
        }

        base.OnExit(e);
    }

    protected async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        try
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

            _contentDialogService ??= _host!.Services.GetRequiredService<IContentDialogService>();

            var result = await _contentDialogService.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions
                {
                    Title = Loc.Instance["Dialog.PendingChanges.Title"],
                    Content = Loc.Instance["Dialog.PendingChanges.Content"],
                    CloseButtonText = Loc.Instance["Dialog.PendingChanges.CloseButton"],
                    PrimaryButtonText = Loc.Instance["Dialog.PendingChanges.PrimaryButton"],
                    SecondaryButtonText = Loc.Instance["Dialog.PendingChanges.SecondaryButton"],
                }
            );

            switch (result)
            {
                case ContentDialogResult.Primary:
                    _logger.LogInformation("User chose to restart PC.");

                    await _host!
                        .Services.GetRequiredService<ShellService>()
                        .QueryCMDAsync("shutdown /r /t 0");
                    _allowClose = true;
                    Current.Shutdown();
                    break;

                case ContentDialogResult.Secondary:
                    _logger.LogInformation("User chose to restart Explorer.");

                    await _host!
                        .Services.GetRequiredService<ShellService>()
                        .QueryCMDAsync("taskkill /f /im explorer.exe && start explorer.exe");
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
            _logger.LogError(
                ex,
                "Failed to handle window closing event. Allowing close to prevent hanging."
            );

            // Ensure we don't block close on error
            _allowClose = true;
            e.Cancel = false;
        }
    }
}
