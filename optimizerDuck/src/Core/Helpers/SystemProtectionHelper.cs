using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Services;
using optimizerDuck.UI;
using optimizerDuck.UI.Components;
using optimizerDuck.UI.Logger;

namespace optimizerDuck.Core.Helpers;

public static class SystemProtectionHelper
{
    private static readonly ILogger Log = Logger.CreateLogger(typeof(SystemProtectionHelper));

    public static bool Enable()
    {
        using var tracker = ServiceTracker.Begin(Log);
        Log.LogInformation("Enabling System Protection...");

        var result = ShellService.PowerShell("Enable-ComputerRestore -Drive \"$env:SystemDrive\"");
        if (result.ExitCode != 0)
        {
            Log.LogError($"[{Theme.Error}]Failed to enable System Protection.[/]");
            return PromptDialog.Warning("Failed to enable System Protection",
                $"""
                 We were [{Theme.Error}]unable[/] to enable System Protection.
                 [{Theme.Muted}]For more information, check the log file.[/]
                 """,
                new PromptOption("Try again", Theme.Warning, Enable),
                new PromptOption("Skip", Theme.Error, () => false)
            );
        }

        Log.LogInformation($"[{Theme.Success}]System Protection enabled successfully.[/]");
        return true;
    }

    public static void Create()
    {
        using var tracker = ServiceTracker.Begin(Log);
        Log.LogInformation("Creating a restore point...");

        var result =
            ShellService.PowerShell(
                $"Checkpoint-Computer -Description \"{Defaults.RestorePointName}\" -RestorePointType MODIFY_SETTINGS");
        if (result.ExitCode != 0)
        {
            Log.LogError($"[{Theme.Error}]Failed to create restore point.[/]");
            PromptDialog.Warning("Failed to create Restore Point",
                $"""
                 We were [{Theme.Error}]unable[/] to create a restore point.
                 [{Theme.Muted}]For more information, check the log file.[/]
                 """,
                new PromptOption("Try again", Theme.Warning, Create),
                new PromptOption("Skip", Theme.Error));
        }
        else
        {
            Log.LogInformation(
                $"[{Theme.Success}]A restore point [{Theme.Primary}]{Defaults.RestorePointName}[/] created successfully.[/]");
        }
    }
}