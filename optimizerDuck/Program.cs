using Microsoft.Extensions.Logging;
using optimizerDuck.Core;
using optimizerDuck.Core.Extensions;
using optimizerDuck.Core.Helpers;
using optimizerDuck.Core.Managers;
using optimizerDuck.Core.Services;
using optimizerDuck.UI;
using optimizerDuck.UI.Components;
using optimizerDuck.UI.Logger;
using Spectre.Console;
using System.Text;

namespace optimizerDuck;

/*
⠀⠀⠀⠀⠀⢀⡄⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⣸⣿⡄⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⢠⡟⠘⣷⣄⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⢰⡟⠀⠀⠈⠻⣷⣤⣤⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⣠⡟⠀⠀⠀⠀⠀⠈⢻⡿⣷⡀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⣰⡟⠀⠀⠀⠀⠀⠀⠀⠀⠻⠸⣷⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⢰⡏⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢹⣇⠀⣠⣄⣀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢀⣀⣀⠀⠀⠀⠀⠀⠀⠀
⣾⡁⢀⣠⠴⠒⠲⣤⣠⠶⠋⠳⣤⣸⣿⣰⣿⣿⣿⣷⣄⠀⠀⠀⠀⠀⠀⠀⠀⣠⣾⣿⣿⣿⡄⠀⠀⠀⠀⠀⠀
⣿⠟⠉⠁⠀⠀⠀⠀⠀⠀⠀⠀⠀⣽⠏⣿⡿⢿⣿⣿⣿⣷⣄⠀⠀⠀⠀⢠⣾⣿⣿⣿⠋⢹⡇⠀⠀⠀⠀⠀⠀
⢹⡆⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣰⡟⠀⣿⠁⠀⠙⣿⡛⠛⢿⡶⠶⠶⠶⣿⣄⣀⣰⠃⠀⢸⡇⠀⠀⠀⠀⠀⠀
⠈⢷⡀⠀⠀⠀⠀⠀⠀⠀⠀⢰⡿⠁⠀⣿⠀⠀⠀⠈⢷⡀⠘⠛⠀⠀⠀⠀⠈⠉⠳⣄⠀⢸⡇⠀⠀⠀⠀⠀⠀
⠀⠈⢿⣦⡀⠀⠀⠀⠀⠀⢀⣿⣇⣀⠀⢻⠀⠀⠀⠀⢰⡇⠀⠀⠀⠀⠀⠀⠀⠀⠀⠈⠳⣾⠃⠀⣤⣤⣄⠀⠀
⠀⠀⠀⠉⠻⢶⣄⣠⣴⠞⠛⠉⠉⠙⠻⢾⣇⠀⢀⣰⠏⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣿⡄⠀⣿⢩⡿⣿⡆
⠀⠀⠀⠀⣠⣴⠟⠉⠀⠀⠀⠀⠀⠀⠀⠀⢹⡷⠞⠁⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢸⡇⠀⣿⣟⣵⡿⠁
⠀⢀⣠⡾⠋⠁⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢸⣇⠀⠀⠀⣴⣿⠀⠀⠀⠀⠀⠀⢠⣶⠀⠀⣸⡇⠀⠙⠋⠁⠀⠀
⢸⣿⣿⡇⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠈⣿⣄⣈⣀⣙⣁⠀⠀⣶⣾⡶⠀⠻⠿⠀⢠⣿⣁⡀⠀⠀⠀⠀⠀
⠈⠛⠻⠿⠶⠶⠶⡤⣤⣤⣤⣄⣀⣤⣀⣠⣤⣀⣀⣹⣿⣿⣿⣿⣤⣽⣿⣴⣶⣶⡦⢼⣿⣿⣿⣿⠇⠀⠀⠀⠀
*/
/// <summary>
///     I started this project on Oct 16, 2025 btw
/// </summary>
internal class Program
{
    private static readonly ILogger Log = Logger.CreateLogger<Program>();
    private OptimizationManager? _optimizer;
    private SystemSnapshot _systemSnapshot = null!;

    private async Task Init()
    {
        RequirementChecker.Restart();

        SystemHelper.Title("Initializing");
        if (!RequirementChecker.IsAdmin())
        {
            Log.LogWarning("Application is not running with administrator privileges.");
            PromptDialog.Warning(
                "Administrator Privileges Required",
                $"""
                 The first initialization to run as admin and maximized seems to have [{Theme.Error}]failed[/].
                 Please restart to grant administrator privileges and ensure the application is maximized.
                 [{Theme.Muted}]You can continue if you have already confirmed your administrator status.[/]
                 """,
                new PromptOption("Restart", Theme.Warning, () => RequirementChecker.Restart(true)),
                new PromptOption("Continue anyway", Theme.Success)
            );
        }


        await AnsiConsole.Status()
            .Spinner(Spinner.Known.BouncingBar)
            .SpinnerStyle($"bold {Theme.Primary}")
            .StartAsyncGlobal("Checking for exclusions...", async ctx =>
            {
                Log.LogInformation("Checking for exclusions...");
                var exclusions = SystemHelper.CheckForExclusions();
                if (exclusions.Count != 0)
                    PromptDialog.Warning("Missing Anti-Malware Exclusions",
                        $"""
                         Do you want to add [{Theme.Primary}]{string.Join($"[/], [{Theme.Primary}]", exclusions)}[/] to the exclusion list?
                         This will help reduce the chances of errors caused by [{Theme.Error}]antivirus blocking[/].
                         """,
                        new PromptOption("Add", Theme.Success, () =>
                        {
                            ctx.Status("Adding exclusions...");
                            SystemHelper.AddToExclusions(exclusions);
                        }),
                        new PromptOption("Skip", Theme.Error));

                ctx.Status("Fetching system information...");
                Log.LogInformation("Fetching system information...");
                _systemSnapshot = await SystemInfoService.RefreshAsync().ConfigureAwait(false);

                Log.LogDebug("=== System Information ===");
                SystemInfoService.GetSummary(Log);
                Log.LogDebug("==========================");

                ctx.Status("Checking and creating necessary paths...");
                Log.LogInformation("Checking and creating necessary paths...");
                SystemHelper.EnsureDirectoriesExists();

                ctx.Status("Checking system requirements...");
                Log.LogInformation("Checking system requirements...");
                RequirementChecker.ValidateSystemRequirements(_systemSnapshot);

                ctx.Status("Initialization completed successfully.");
                Log.LogInformation($"[{Theme.Success}]Initialization completed successfully.[/]");
            });
    }

    private async Task Start()
    {
        while (true)
        {
            SystemHelper.Title(
                $"{_systemSnapshot.Os.Name} {_systemSnapshot.Os.Edition} ({_systemSnapshot.Os.Architecture})");
            AnsiConsole.Clear();
            AnsiConsole.Write(Defaults.Logo);

            AnsiConsole.WriteLine();

            var start = Align.Center(
                new PromptPanel('E', "Start Optimization",
                    $"""
                     [bold]Customize your optimization process.[/]
                     Review available tweaks, select what fits your needs,
                     and apply them to fine-tune the system.

                     [bold {Theme.Warning}]Some changes may require a restart.[/]
                     [dim white]Network connectivity might be needed for certain tweaks.[/]
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

            while (true)
            {
                Console.CursorVisible = false;
                var option = Console.ReadKey(true).KeyChar;
                Console.CursorVisible = true;

                if (option == 'e')
                {
                    _optimizer ??= new OptimizationManager(_systemSnapshot);
                    if (await _optimizer.Begin().ConfigureAwait(false))
                        if (await OptimizationManager.RestorePoint().ConfigureAwait(false))
                            await _optimizer.Run().ConfigureAwait(false);

                    break;
                }

                if (option == 'd')
                {
                    SystemHelper.OpenWebsite(Defaults.DiscordInvite);
                    break;
                }

                if (option == 'g')
                {
                    SystemHelper.OpenWebsite(Defaults.GitHubRepo);
                    break;
                }

                if (option == 'i')
                {
                    Log.LogInformation("Refreshing System Information...");
                    _systemSnapshot = await SystemInfoService.RefreshAsync().ConfigureAwait(false);

                    AnsiConsole.Clear();
                    AnsiConsole.Write(Defaults.Logo);
                    AnsiConsole.Write(SystemInfoService.GetDetailedPanel(_systemSnapshot));
                    AnsiConsole.Write(
                        Align.Center(
                            new Markup("[dim]Press any key to go back to the main menu.[/]"),
                            VerticalAlignment.Middle)
                    );
                    Console.ReadKey();
                    break;
                }
            }
        }
    }

    private static async Task Main(string[] _)
    {
        try
        {
            Console.OutputEncoding =
                Encoding.UTF8; // idk why, but it will fix some ansi issues of spectre console

            AnsiConsole.Clear();
            AnsiConsole.Write(Defaults.Logo);

            var app = new Program();
            await app.Init().ConfigureAwait(false);
            await app.Start().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.LogCritical(ex, $"[{Theme.Error}]Fatal error![/]");
            PromptDialog.Exception(ex, "Fatal Error",
                "An unexpected error occurred while running the application.");
        }
    }
}