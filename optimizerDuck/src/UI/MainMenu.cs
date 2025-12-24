using optimizerDuck.Core;
using optimizerDuck.Core.Helpers;
using optimizerDuck.Core.Services;
using optimizerDuck.UI.Components;
using Spectre.Console;

namespace optimizerDuck.UI;

internal static class MainMenu
{
    public static void Display(SystemSnapshot systemSnapshot)
    {
        SystemHelper.Title(
            $"{systemSnapshot.Os.Name} {systemSnapshot.Os.Edition} ({systemSnapshot.Os.Architecture})");
        AnsiConsole.Clear();
        AnsiConsole.Write(Defaults.Logo);

        AnsiConsole.WriteLine();

        var start = Align.Center(
            new PromptPanel('E', "Start Optimization",
                $"""
                 [bold]Customize your optimization process.[/]
                 Browse the available options, pick the ones that suit you best,
                 and apply them to improve your system.

                 [bold {Theme.Warning}]Some changes may require a restart.[/]
                 [dim white]Certain options may also need an internet connection.[/]
                 """,
                Theme.Primary,
                CustomBoxBorder.LeftBorder).Build()
        );

        var middlePanels = Align.Center(
            new Columns(
                    new PromptPanel('D', "Join our Discord",
                        $"""
                         [bold]{Emoji.Known.SpeechBalloon} Get help, share ideas, and stay updated.[/]
                         Connect with the community and the dev team directly.
                         """,
                        Theme.Info,
                        CustomBoxBorder.UnderlineBorder).Build(),
                    new PromptPanel('G', "GitHub",
                        $"""
                         [bold]{Emoji.Known.Laptop} Explore the source code and releases.[/]
                         Contribute, report issues, or check for the latest updates.
                         """,
                        "white",
                        CustomBoxBorder.UnderlineBorder).Build(),
                    new PromptPanel('I', "System Information",
                        $"""
                         [bold]{Emoji.Known.Gear}  View system information.[/]
                         Review detailed system information and diagnostics.
                         """,
                        Theme.Accent,
                        CustomBoxBorder.UnderlineBorder).Build()
                )
                { Expand = false }
        );
        AnsiConsole.Write(start);
        AnsiConsole.Write(middlePanels);

        AnsiConsole.WriteLine();
    }
}