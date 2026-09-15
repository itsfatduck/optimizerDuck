using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Optimizations.Models.Services;

namespace optimizerDuck.Services.System.Primitives;

/// <summary>
///     Shared mapping between <see cref="ProcessResult"/> and <see cref="ShellResult"/>, plus the
///     command-building helpers used by <c>ShellService</c>.
/// </summary>
internal static class ShellMapping
{
    internal const string PowerShellArguments =
        "-NonInteractive -NoLogo -NoProfile -ExecutionPolicy Bypass -EncodedCommand";

    internal static string PrefixPowerShell(string command)
    {
        // prefix forces UTF-8 output and silences progress stream clutter in stderr
        return "$ProgressPreference='SilentlyContinue'; "
            + "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; "
            + "$OutputEncoding = [System.Console]::OutputEncoding = [System.Console]::InputEncoding = [System.Text.Encoding]::UTF8; "
            + command;
    }

    internal static string BuildCmdArguments(string arguments, string command)
    {
        return arguments.Contains("chcp 65001")
            ? // best effort: force UTF-8 codepage for cmd
            $"{arguments} {command}"
            : $"{arguments} chcp 65001 > nul & {command}";
    }

    internal static string EncodePowerShellCommand(string command) =>
        PrefixPowerShell(command).EncodeBase64();

    internal static ShellResult Map(ProcessResult raw, string commandForUser) =>
        new()
        {
            Command = commandForUser,
            Stdout = raw.Stdout,
            Stderr = raw.Stderr.ParseCliXml().Trim(),
            ExitCode = raw.TimedOut ? -1 : raw.ExitCode,
            Duration = raw.Duration,
        };
}
