using Microsoft.Extensions.Logging;
using optimizerDuck.Core;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Parsing;
using Spectre.Console;
using Vertical.SpectreLogger;
using Vertical.SpectreLogger.Options;
using Vertical.SpectreLogger.Rendering;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace optimizerDuck.UI.Logger;

public class PlainTextFormatter : ITextFormatter
{
    public void Format(LogEvent logEvent, TextWriter output)
    {
        // [2025/10/30 21:15:43]
        output.Write($"[{logEvent.Timestamp:yyyy/MM/dd HH:mm:ss}]");

        // [SourceContext]
        if (logEvent.Properties.TryGetValue("SourceContext", out var ctx) && ctx is ScalarValue sv)
            output.Write($" [{sv.Value,-25}]");



        // [INFO   ]
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
        output.Write($" [{levelText,-7}]");

        // [Scope1] [Scope2] ...
        if (logEvent.Properties.TryGetValue("Scope", out var scopeValue))
        {
            if (scopeValue is SequenceValue seq)
                foreach (var scope in seq.Elements)
                {
                    var text = scope switch
                    {
                        ScalarValue s => s.Value?.ToString(),
                        _ => scope.ToString()
                    };

                    if (!string.IsNullOrWhiteSpace(text))
                        output.Write($" [{text}]");
                }
            else if (scopeValue is ScalarValue single) output.Write($" [{single.Value}]");
        }


        // Message
        var message = RenderWithoutQuotes(logEvent);
        if (logEvent.Level == LogEventLevel.Debug && !Defaults.IsDebug) // i want to keep markup for debug logs when not in debug mode to avoid markup exceptions
            output.Write($" {message}");
        else
            output.Write($" {Markup.Remove(message)}");

        // Include exception details if present
        if (logEvent.Exception != null)
        {
            output.WriteLine();
            output.Write(logEvent.Exception.ToString());
        }

        // Add a new line at the end of the log event
        output.WriteLine();
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

public class ShortSourceContextEnricher : ILogEventEnricher
{
    //private readonly string _rootNamespace = Assembly.GetEntryAssembly()?.GetName().Name?.Replace(' ', '_') + ".";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        if (logEvent.Properties.TryGetValue("SourceContext", out var value))
        {
            //Console.WriteLine(_rootNamespace);

            var fullName = value.ToString().Trim('"');


            // if the source context is in the root namespace, remove it
            // var name = fullName.StartsWith(_rootNamespace)
            //     ? fullName.Substring(_rootNamespace.Length)
            //     : fullName;

            var name = fullName.Split('.').Last();

            logEvent.AddOrUpdateProperty(factory.CreateProperty("SourceContext", name));
        }
    }
}

public static class Logger
{
    public static readonly string LogFilePath = Path.Combine(Defaults.RootPath, "optimizerDuck.log");
    private static readonly ILoggerFactory _loggerFactory;

    static Logger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.With<ShortSourceContextEnricher>()
            .WriteTo.File(new PlainTextFormatter(), LogFilePath, shared: true)
            .CreateLogger();

        // add a header to the log file
        Log.Logger.Information("\n\n{Logo}\nVersion: {Version}\n\n", Defaults.RawLogo, Defaults.FileVersion);

        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSpectreConsole(config =>
            {
                config.SetMinimumLevel(Defaults.IsDebug
                    ? LogLevel.Debug
                    : LogLevel.Information);

                config.ConfigureProfiles(profile =>
                {
                    // https://github.com/verticalsoftware/vertical-spectreconsolelogger/issues/31
                    profile.PreserveMarkupInFormatStrings = true;

                    // https://github.com/verticalsoftware/vertical-spectreconsolelogger/blob/dev/src/Options/SerilogStyleLoggerOptions.cs
                    // https://github.com/verticalsoftware/vertical-spectreconsolelogger/blob/dev/docs/styling.md
                    profile
                        .AddTypeFormatter<LogLevel>((_, value, _) =>
                            value switch
                            {
                                LogLevel.Trace => "TRACE",
                                LogLevel.Debug => "DEBUG",
                                LogLevel.Information => "INFO",
                                LogLevel.Warning => "WARNING",
                                LogLevel.Error => "ERROR",
                                LogLevel.Critical => "CRITICAL",
                                _ => "UNKNOWN"
                            })
                        .AddTypeStyle<int>($"[{Theme.Primary}]")
                        .AddTypeStyle<string>($"[{Theme.Primary}]")
                        .AddTypeStyle<DateTimeRenderer.Value>($"[{Theme.Muted}]");

                    profile.OutputTemplate = "{DateTime:yyyy/MM/dd HH:mm:ss} {LogLevel,-7} {Message}{NewLine}";
                    profile.DefaultLogValueStyle = $"[{Theme.Primary}]";
                });
                config.ConfigureProfile(LogLevel.Information,
                    profile => profile.AddTypeStyle<LogLevel>($"[{Theme.Info}]"));
                config.ConfigureProfile(LogLevel.Debug,
                    profile => profile.AddTypeStyle<LogLevel>($"[{Theme.Warning}]"));
                config.ConfigureProfile(LogLevel.Warning,
                    profile => profile.AddTypeStyle<LogLevel>($"[{Theme.Warning}]"));
                config.ConfigureProfile(LogLevel.Error, profile => profile.AddTypeStyle<LogLevel>($"[{Theme.Error}]"));
                config.ConfigureProfile(LogLevel.Critical,
                    profile => profile.AddTypeStyle<LogLevel>($"[{Theme.Error}]"));
            });
            builder.AddSerilog(Log.Logger);
        });
    }

    public static ILogger CreateLogger<T>()
    {
        return _loggerFactory.CreateLogger<T>();
    }

    public static ILogger CreateLogger(Type type)
    {
        return _loggerFactory.CreateLogger(type);
    }
}