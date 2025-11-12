using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Helpers;
using optimizerDuck.Core.Managers;
using optimizerDuck.Core.Services;
using optimizerDuck.src.Core;
using optimizerDuck.UI.Logger;
using Spectre.Console;

namespace optimizerDuck.src.UI;

internal class MenuHandler
{
    private readonly ILogger _log = Logger.CreateLogger<MenuHandler>();
    private readonly Dictionary<char, Func<Task>> _menuActions;

    private OptimizationManager? _optimizer;
    private SystemSnapshot _systemSnapshot;

    public MenuHandler(SystemSnapshot systemSnapshot)
    {
        _systemSnapshot = systemSnapshot;
        _menuActions = new Dictionary<char, Func<Task>>
        {
            { 'e', HandleStartOptimizationAsync },
            { 'd', HandleDiscordLinkAsync },
            { 'g', HandleGitHubLinkAsync },
            { 'i', HandleSystemInfoAsync }
        };
    }

    public async Task<SystemSnapshot> HandleInputAsync(char option)
    {
        if (_menuActions.TryGetValue(option, out var action))
        {
            await action();
        }

        // If invalid key, do nothing and let the loop re-render the menu.
        return _systemSnapshot;
    }

    private async Task HandleStartOptimizationAsync()
    {
        _optimizer ??= new OptimizationManager(_systemSnapshot);
        if (await _optimizer.Begin().ConfigureAwait(false))
            if (await OptimizationManager.RestorePoint().ConfigureAwait(false))
                await _optimizer.Run().ConfigureAwait(false);
    }

    private Task HandleDiscordLinkAsync()
    {
        SystemHelper.OpenWebsite(Defaults.DiscordInvite);
        return Task.CompletedTask;
    }

    private Task HandleGitHubLinkAsync()
    {
        SystemHelper.OpenWebsite(Defaults.GitHubRepo);
        return Task.CompletedTask;
    }

    private async Task HandleSystemInfoAsync()
    {
        _log.LogInformation("Refreshing System Information...");
        _systemSnapshot = await SystemInfoService.RefreshAsync().ConfigureAwait(false);

        AnsiConsole.Clear();
        AnsiConsole.Write(Defaults.Logo);

        var detailedPanel = SystemInfoService.GetDetailedPanel(_systemSnapshot, _log);
        if (detailedPanel is not null)
        {
            AnsiConsole.Write(detailedPanel);
            AnsiConsole.Write(
                Align.Center(
                    new Markup("[dim]Press any key to go back to the main menu.[/]"),
                    VerticalAlignment.Middle)
            );
            Console.ReadKey();
        }
    }
}
