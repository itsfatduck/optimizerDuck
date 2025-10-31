using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Extensions;
using optimizerDuck.UI.Logger;
using Spectre.Console;
using System.Diagnostics;
using System.Text;

namespace optimizerDuck.Core.Services;

public record ShellResult(
    string Command,
    string Stdout,
    string Stderr,
    int ExitCode,
    TimeSpan Duration);

public static class ShellService
{
    private static ShellResult Run(string fileName, string arguments, string command, string serviceName)
    {
        var fullCommand =
            $"{fileName} {arguments.Replace("-EncodedCommand", "-Command")} {DecodeBase64(command)}"; // let user see the real command

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = $"{arguments} {command}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };

        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            stdoutBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            stderrBuilder.AppendLine(e.Data);
        };
        process.Start();

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit();

        var duration = process.ExitTime - process.StartTime;

        var stdout = Markup.Escape(stdoutBuilder.ToString().Trim());
        var stderr = stderrBuilder.ToString().ParseCliXml().Trim();

        var success = process.ExitCode == 0;

        var stdoutDisplay = string.IsNullOrWhiteSpace(stdout) ? "N/A" : stdout;
        var stderrDisplay = string.IsNullOrWhiteSpace(stderr) ? "N/A" : stderr;

        ServiceTracker.Current?.Track(serviceName, success);
        ServiceTracker.Current?.Log.LogDebug(
            """
            {FullCommand}
            | Stdout: {Stdout}
            | Stderr: {Stderr}
            | ExitCode: {ProcessExitCode}
            | Duration: {TimeSpan}
            """, fullCommand, stdoutDisplay, stderrDisplay, process.ExitCode, ServiceTracker.FormatTime(duration));

        return new ShellResult(
            $"{fileName} {arguments} {command}",
            stdoutDisplay,
            stderrDisplay,
            process.ExitCode,
            duration);
    }

    public static ShellResult CMD(string command)
    {
        return Run("cmd.exe", "/c", command, nameof(CMD));
    }

    public static ShellResult PowerShell(string command)
    {
        command = "$ProgressPreference='SilentlyContinue'; " + command; // to hide progress bar clixml
        return Run("powershell.exe",
            "-NonInteractive -NoLogo -NoProfile -ExecutionPolicy Bypass -EncodedCommand",
            EncodePowerShellCommand(command),
            nameof(PowerShell));
    }

    /// <summary>
    ///     encode command to base64 for better script handling
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    private static string EncodePowerShellCommand(string command)
    {
        var bytes = Encoding.Unicode.GetBytes(command);
        return Convert.ToBase64String(bytes);
    }

    private static string DecodeBase64(string value)
    {
        try
        {
            var bytes = Convert.FromBase64String(value);
            return Encoding.Unicode.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }
}