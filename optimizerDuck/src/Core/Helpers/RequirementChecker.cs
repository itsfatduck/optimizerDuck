using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Services;
using optimizerDuck.UI;
using optimizerDuck.UI.Components;
using optimizerDuck.UI.Logger;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace optimizerDuck.Core.Helpers;

public static class RequirementChecker
{
    private static readonly ILogger Log = Logger.CreateLogger(typeof(RequirementChecker));

    public static bool IsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void ValidateSystemRequirements(SystemSnapshot s)
    {
        if (!OperatingSystem.IsWindows())
        {
            Log.LogWarning("Untested operating system.");

            PromptDialog.Warning(
                "Untested Operating System",
                """
        This optimizer has not been tested on non-Windows operating systems.
        You may continue at your own risk, but some features may not work as expected.
        """,
                new PromptOption("Exit", Theme.Error, () => Environment.Exit(1)),
                new PromptOption("Continue anyway", Theme.Warning)
            );
        }

        if (int.TryParse(s.Os.Version, out var version) && version < 10)
        {
            Log.LogWarning("Untested Windows version.");

            PromptDialog.Warning(
                "Untested Windows Version",
                $"""
        Your Windows version [{Theme.Error}]{s.Os.Version}[/] has not been tested with this optimizer.
        It is recommended to use Windows 10 or later.
        [{Theme.WarningMuted}]You may continue anyway, but some features may not behave as expected.[/]
        """,
                new PromptOption("Exit", Theme.Error, () => Environment.Exit(1)),
                new PromptOption("Continue anyway", Theme.Warning)
            );
        }

        if (!Environment.Is64BitOperatingSystem)
        {
            Log.LogWarning("Untested operating system architecture.");

            PromptDialog.Warning(
                "Untested Operating System Architecture",
                $"""
        Your system architecture [{Theme.Error}]{s.Os.Architecture}[/] has not been tested.
        This optimizer is designed for 64-bit Windows systems.
        [{Theme.WarningMuted}]You may continue anyway, but some features may not work properly.[/]
        """,
                new PromptOption("Exit", Theme.Error, () => Environment.Exit(1)),
                new PromptOption("Continue anyway", Theme.Warning)
            );
        }
    }

    public static void EnsureDirectoriesExists()
    {
        if (!Directory.Exists(Defaults.RootPath))
        {
            Log.LogInformation(@"AppData\optimizerDuck directory does not exist. Creating directory at: {Path}",
                Defaults.RootPath);
            Directory.CreateDirectory(Defaults.RootPath);
        }

        if (!Directory.Exists(Defaults.ResourcesPath))
        {
            Log.LogInformation("Resources directory does not exist. Creating directory at: {Path}",
                Defaults.ResourcesPath);
            Directory.CreateDirectory(Defaults.ResourcesPath);
        }
    }

    public static void Restart(bool force = false)
    {
        var args = Environment.GetCommandLineArgs();
        if (force || !args.Contains("--restart"))
            try
            {
                SystemHelper.Title("Restarting...");
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Environment.ProcessPath,
                        Arguments = $"--restart {string.Join(" ", args.Skip(1))}",
                        WindowStyle = ProcessWindowStyle.Maximized,
                        WorkingDirectory = Environment.CurrentDirectory,
                        UseShellExecute = true,
                        Verb = "runas"
                    }
                };
                Log.LogInformation("Restarting application with administrator privileges and maximized window.");
                Log.LogInformation(
                    $"Please press [{Theme.Success}]Yes[/] if [{Theme.Primary}]User Account Control[/] (UAC) prompt appears.");

                process.Start();
                Log.LogInformation("Operation completed successfully! Exiting...");
                Environment.Exit(0);
            }
            catch (Win32Exception ex) when
                (ex.ErrorCode == -2147467259) // The operation was canceled by the user.
            {
                Log.LogError("User [red]declined[/] the elevation prompt.");

                PromptDialog.Warning(
                    "Administrator Privileges Required",
                    $"""
                     To run properly, this application needs administrator rights and should open in full screen.
                     Restart the app and click [{Theme.Success}]Yes[/] if [{Theme.Primary}]User Account Control[/] (UAC) prompt appears.
                     """,
                    new PromptOption("Restart", Theme.Success, () => Restart(true)),
                    new PromptOption("Exit", Theme.Error, () => Environment.Exit(1))
                );
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "Failed to restart process");

                PromptDialog.Exception(ex,
                    "Restart Failed",
                    "An error occurred while trying to restart with administrator privileges."
                );
            }
    }
}