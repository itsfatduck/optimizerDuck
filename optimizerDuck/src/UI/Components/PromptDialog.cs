using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Helpers;
using optimizerDuck.UI.Logger;
using Spectre.Console;

namespace optimizerDuck.UI.Components;

public record PromptOption
{
    public PromptOption(string key, string color, Action? action = null)
    {
        Key = key;
        Color = color;
        if (action != null)
            OnSelected = () =>
            {
                action();
                return true;
            }; // default to return true
        else
            OnSelected = () => true;
    }

    public PromptOption(string key, string color, Func<bool> func)
    {
        Key = key;
        Color = color;
        OnSelected = func;
    }

    public string Key { get; }
    public string Color { get; }
    public Func<bool> OnSelected { get; }
}

public class PromptDialog
{
    private static readonly ILogger Log = Logger.Logger.CreateLogger<PromptDialog>();
    private static string _previousTitle = "";

    private readonly List<PromptOption> _options = [];
    private string _borderColor = "grey";
    private string _description = "";

    private string _title = "";
    private string _titleColor = "white";

    public PromptDialog Title(string title)
    {
        _title = title;
        return this;
    }

    public PromptDialog TitleColor(string color)
    {
        _titleColor = color;
        return this;
    }

    public PromptDialog Description(string description)
    {
        _description = description;
        return this;
    }

    public PromptDialog AddOption(params PromptOption[] options)
    {
        _options.AddRange(options);
        return this;
    }

    public PromptDialog BorderColor(string color)
    {
        _borderColor = color;
        return this;
    }

    private string Prompt()
    {
        var buttonList = new List<string>();
        var keyMap = new Dictionary<char, string>(); // "Yes" => Y: Yes
        foreach (var (option, key, rest, color) in from option in _options
                 let key = char.ToUpper(option.Key[0])
                 let rest = option.Key.Substring(1)
                 let color = option.Color
                 select (option, key, rest, color))
        {
            buttonList.Add($"[{color}][underline bold]{key}[/]{rest}[/]");
            keyMap[key] = option.Key;
        }

        var buttons = string.Join("   ", buttonList);

        var panelContent = new Rows(
            new Markup(_description).Centered(),
            new Text("\n"),
            new Markup(buttons).Centered()
        );
        var panel = new Panel(panelContent)
        {
            Header = new PanelHeader($"[bold {_titleColor}] {_title} [/]", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = _borderColor,
            Padding = new Padding(5, 1, 5, 1)
        };
        AnsiConsole.Write(Align.Center(panel));
        Console.Beep();

        _previousTitle = Console.Title;
        SystemHelper.Title(_title);


        using (new StatusContextScope(
                   GlobalStatus.Current,
                   Spinner.Known.SimpleDotsScrolling,
                   _borderColor,
                   "Select an option (press its first character)"))
        {
            while (true)
            {
                Console.CursorVisible = false;
                var keyInfo = Console.ReadKey(true);
                Console.CursorVisible = true;

                if (!char.IsLetter(keyInfo.KeyChar)) continue;


                var pressed = char.ToUpper(keyInfo.KeyChar);

                if (keyMap.TryGetValue(pressed, out var value))
                {
                    Log.LogDebug("[{Title}] User selected option: {Option}", _title, value);
                    Console.Title = _previousTitle;
                    return value;
                }

                AnsiConsole.MarkupLine("[red]Invalid key![/] [dim]Press the first character of the option![/]");
            }
        }
    }


    public static bool Warning(string title, string description, params PromptOption[] options)
    {
        var panel = new PromptDialog()
            .Title(title)
            .Description(description);

        var finalOptions = options is { Length: > 0 }
            ? options
            :
            [
                new PromptOption("Continue anyway", Theme.Success),
                new PromptOption("Exit", Theme.Error, () => Environment.Exit(1))
            ];

        panel.AddOption(finalOptions)
            .TitleColor($"black on {Theme.Warning}")
            .BorderColor(Theme.WarningMuted);

        var choice = panel.Prompt();
        var selected = finalOptions.FirstOrDefault(o => o.Key == choice)!;

        return selected!.OnSelected!.Invoke();
    }


    public static void Exception(Exception exception, string title, string description, params PromptOption[] options)
    {
        var panel = new PromptDialog()
            .Title(title)
            .Description($"""
                          {description}

                          [bold {Theme.Error}]Exception:[/]
                            [italic]{exception.GetType().Name}[/]
                            [bold {Theme.Error}]{Markup.Escape(exception.Message)}[/]

                          [dim]──────────────────────────────[/]
                          [grey]Please check the log file for more details.[/]
                          """);

        var finalOptions = options is { Length: > 0 }
            ? options
            :
            [
                new PromptOption("Exit", Theme.Error, () => Environment.Exit(1)),
                new PromptOption("Restart", Theme.Success, () => RequirementChecker.Restart(true)),
                new PromptOption("Create a new issue on GitHub", Theme.Bright,
                    () => SystemHelper.OpenWebsite("https://github.com/itsfatduck/optimizerDuck/issues/new")),
                new PromptOption("Open log", Theme.Warning, SystemHelper.OpenLogFile)
            ];

        panel.AddOption(finalOptions)
            .TitleColor($"black on {Theme.Error}")
            .BorderColor(Theme.ErrorMuted);

        var choice = panel.Prompt();

        var selected = finalOptions.FirstOrDefault(o => o.Key == choice);
        selected!.OnSelected.Invoke();
    }
}