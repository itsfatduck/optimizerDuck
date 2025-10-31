using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Services;
using optimizerDuck.UI;
using optimizerDuck.UI.Components;
using optimizerDuck.UI.Logger;

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
            Log.LogWarning("Unsupported operating system.");

            PromptDialog.Warning(
                "Unsupported Operating System",
                """
                Currently, this optimizer doesn't support non-Windows operating systems.
                Please use a supported version of Windows to continue.
                """,
                new PromptOption("Exit", Theme.Error, () => Environment.Exit(1))
            );
        }

        if (int.TryParse(s.Os.Version, out var version) && version < 10) // only >= Windows 10 supported
        {
            Log.LogWarning("Unsupported operating system.");

            PromptDialog.Warning(
                "Unsupported Operating System",
                $"""
                 Your operating system version [{Theme.Error}]{s.Os.Version}[/] is not supported.
                 Please upgrade to Windows 10 or later to use this application.
                 [{Theme.WarningMuted}]If you still want to continue, you can choose to do so, but keep in mind that some features may not work as expected.[/]
                 """,
                new PromptOption("Exit", Theme.Error, () => Environment.Exit(1)),
                new PromptOption("Continue anyways", Theme.Warning)
            );
        }

        if (!Environment.Is64BitOperatingSystem) // only 64-bit supported
        {
            Log.LogWarning("Unsupported operating system architecture.");
            PromptDialog.Warning(
                "Unsupported Operating System Architecture",
                $"""
                 Your operating system architecture [{Theme.Error}]{s.Os.Architecture}[/] is not supported.
                 Please upgrade to a 64-bit version of Windows to use this application.
                 [{Theme.WarningMuted}]If you still want to continue, you can choose to do so, but keep in mind that some features may not work as expected.[/]
                 """,
                new PromptOption("Exit", Theme.Error, () => Environment.Exit(1)),
                new PromptOption("Continue anyways", Theme.Warning)
            );
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
                Log.LogWarning(
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