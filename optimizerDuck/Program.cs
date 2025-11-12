using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Extensions;
using optimizerDuck.Core.Helpers;
using optimizerDuck.Core.Services;
using optimizerDuck.src.Core;
using optimizerDuck.src.UI;
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
        var menuHandler = new MenuHandler(_systemSnapshot);

        while (true)
        {
            MainMenu.Display(_systemSnapshot);

            Console.CursorVisible = false;
            var option = Console.ReadKey(true).KeyChar;
            Console.CursorVisible = true;

            _systemSnapshot = await menuHandler.HandleInputAsync(option).ConfigureAwait(false);
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

            await UpdateService.CheckForUpdatesAsync();

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