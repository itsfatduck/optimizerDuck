using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Core.Models.Config;
using optimizerDuck.Core.Models.Execution;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Core.Models.Revert.Steps;
using optimizerDuck.Resources.Languages;

namespace optimizerDuck.Services.OptimizationServices;

public sealed class ShellPolicy
{
    public static readonly ShellPolicy Default = new();

    public Func<ShellResult, bool> IsSuccess { get; init; } = r => r.ExitCode == 0;

    public Func<ShellResult, string?> ErrorFactory { get; init; } =
        r => r.ExitCode == -1 // if timed out use Error.TimedOut
            ? Translations.Service_Shell_Error_TimedOut
            : string.IsNullOrWhiteSpace(r.Stderr) // else check stderr, if empty use exitcode, else use raw stderr
                ? string.Format(Translations.Service_Shell_Error_ExitCode, r.ExitCode)
                : r.Stderr;

    public static ShellPolicy From(Func<ShellResult, bool> isSuccess, Func<ShellResult, string?>? errorFactory = null)
    {
        return new ShellPolicy
        {
            IsSuccess = isSuccess,
            ErrorFactory = errorFactory ?? Default.ErrorFactory
        };
    }

    public static ShellPolicy SuccessExitCodes(params int[] okExitCodes)
    {
        return From(r => okExitCodes.Contains(r.ExitCode));
    }

    public static ShellPolicy SuccessExitCodeRange(int maxOk)
    {
        return From(r => r.ExitCode >= 0 && r.ExitCode <= maxOk);
    }
}

public static class ShellService
{
    private static IOptionsMonitor<AppSettings>? _options;

    public static void Init(IOptionsMonitor<AppSettings> options)
    {
        _options = options;
    }

    private static (string commandForUser, string fullCommandForUser) SanitizeCommandForUser(
        string fileName, string arguments, string command)
    {
        var commandForUser = (arguments.Contains("-EncodedCommand", StringComparison.OrdinalIgnoreCase)
                ? command.DecodeBase64()
                : command)
            .Replace("$ProgressPreference='SilentlyContinue'; ", "")
            .Replace("[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; ", "")
            .Replace(
                "$OutputEncoding = [System.Console]::OutputEncoding = [System.Console]::InputEncoding = [System.Text.Encoding]::UTF8; ",
                "")
            .Replace("chcp 65001 > nul & ", "");

        var fullCommandForUser =
            $"{fileName} {arguments.Replace("-EncodedCommand", "-Command", StringComparison.OrdinalIgnoreCase)} {commandForUser}";

        return (commandForUser, fullCommandForUser);
    }

    private static ShellResult Run(
        string fileName,
        string arguments,
        string command,
        string serviceName,
        ShellRevertStep? revertStep,
        ShellPolicy? policy = null)
    {
        policy ??= ShellPolicy.Default;

        var (_, fullCommandForUser) = SanitizeCommandForUser(fileName, arguments, command);

        var processArgs =
            fileName.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase) &&
            !arguments.Contains(
                "chcp 65001") // try best effort to ensure UTF-8 codepage for cmd, but better use PowerShell if possible
                ? $"{arguments} chcp 65001 > nul & {command}"
                : $"{arguments} {command}";

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = processArgs,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = new Process();
            process.StartInfo = psi;

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
            var sw = Stopwatch.StartNew();

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var exited =
                process.WaitForExit(_options?.CurrentValue.Optimize.ShellTimeoutMs ?? 120000); // fallback to 2 minutes
            var timedOut = !exited;

            if (timedOut)
            {
                try
                {
                    process.Kill(true);
                }
                catch
                {
                    ExecutionScope.LogError(null, "Failed to kill process for timeout");
                }

                process.WaitForExit(2000); // grace drain
            }

            sw.Stop();
            var duration = sw.Elapsed;
            var stdout = stdoutBuilder.ToString().Trim();
            var stderr = stderrBuilder.ToString().ParseCliXml().Trim();

            var result = new ShellResult
            {
                Command = fullCommandForUser,
                Stdout = stdout,
                Stderr = stderr,
                ExitCode = timedOut ? -1 : process.ExitCode, // use -1 error code for timed out
                Duration = duration
            };

            var success = policy.IsSuccess(result);

            ExecutionScope.Track(serviceName, success);

            ExecutionScope.LogInfo("[{Service}][{Status}][EC={ExitCode}][D={Duration}] {Command}",
                serviceName,
                timedOut ? "TIMEOUT" : success ? "OK" : "FAIL",
                result.ExitCode,
                duration.FormatTime(),
                fullCommandForUser);

            ExecutionScope.LogTrace("[{Service}][STDOUT] {Stdout}",
                serviceName,
                string.IsNullOrWhiteSpace(stdout) ? "N/A" : stdout
            );
            ExecutionScope.LogTrace("[{Service}][STDERR] {Stderr}",
                serviceName,
                string.IsNullOrWhiteSpace(stderr) ? "N/A" : stderr
            );

            var error = success ? null : policy.ErrorFactory(result);

            ExecutionScope.RecordStep(
                Translations.Service_Shell_Name,
                fullCommandForUser,
                success,
                revertStep,
                error,
                success
                    ? null
                    : () => Task.Run(() =>
                    policy.IsSuccess(Run(fileName, arguments, command, serviceName, revertStep, policy))
                    )
            );

            return result;
        }
        catch (Exception ex)
        {
            ExecutionScope.LogError(ex, "[{Service}][FAIL][EXCEPTION] {Command}",
                serviceName, fullCommandForUser);
            ExecutionScope.Track(serviceName, false);

            var result = new ShellResult
            {
                Command = fullCommandForUser,
                Stdout = string.Empty,
                Stderr = ex.Message,
                ExitCode = -2,
                Duration = TimeSpan.Zero
            };

            ExecutionScope.RecordStep(
                Translations.Service_Shell_Name,
                fullCommandForUser,
                false,
                revertStep,
                ex.Message,
                () => Task.Run(() =>
                    policy.IsSuccess(Run(fileName, arguments, command, serviceName, revertStep, policy))
                    )
                );

            return result;
        }
    }

    private static async Task<ShellResult> RunAsync(
        string fileName,
        string arguments,
        string command,
        string serviceName,
        ShellRevertStep? revertStep,
        ShellPolicy? policy = null,
        CancellationToken ct = default)
    {
        policy ??= ShellPolicy.Default;

        var (_, fullCommandForUser) = SanitizeCommandForUser(fileName, arguments, command);

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

        try
        {
            using var process = new Process();
            process.StartInfo = psi;

            var stdoutBuilder = new StringBuilder();
            var stderrBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) stdoutBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) stderrBuilder.AppendLine(e.Data);
            };

            var sw = Stopwatch.StartNew();

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var timeout = _options?.CurrentValue.Optimize.ShellTimeoutMs ?? 120000;

            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, ct);

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(true);
                }
                catch
                {
                }
            }

            sw.Stop();

            var timedOut = !process.HasExited;
            if (!process.HasExited)
                process.WaitForExit(2000);

            var stdout = stdoutBuilder.ToString().Trim();
            var stderr = stderrBuilder.ToString().ParseCliXml().Trim();

            var result = new ShellResult
            {
                Command = fullCommandForUser,
                Stdout = stdout,
                Stderr = stderr,
                ExitCode = timedOut ? -1 : process.ExitCode,
                Duration = sw.Elapsed
            };

            var success = policy.IsSuccess(result);

            ExecutionScope.Track(serviceName, success);

            ExecutionScope.LogInfo("[{Service}][{Status}][EC={ExitCode}][D={Duration}] {Command}",
                serviceName,
                timedOut ? "TIMEOUT" : success ? "OK" : "FAIL",
                result.ExitCode,
                sw.Elapsed.FormatTime(),
                fullCommandForUser);

            ExecutionScope.LogTrace("[{Service}][STDOUT] {Stdout}",
                serviceName,
                string.IsNullOrWhiteSpace(stdout) ? "N/A" : stdout);

            ExecutionScope.LogTrace("[{Service}][STDERR] {Stderr}",
                serviceName,
                string.IsNullOrWhiteSpace(stderr) ? "N/A" : stderr);

            var error = success ? null : policy.ErrorFactory(result);

            ExecutionScope.RecordStep(
                Translations.Service_Shell_Name,
                fullCommandForUser,
                success,
                revertStep,
                error,
                success
                    ? null
                    : async () => policy.IsSuccess(await RunAsync(fileName, arguments, command, serviceName, revertStep, policy, ct))
                    );

            return result;
        }
        catch (Exception ex)
        {
            ExecutionScope.LogError(ex, "[{Service}][FAIL][EXCEPTION] {Command}",
                serviceName, fullCommandForUser);
            ExecutionScope.Track(serviceName, false);

            var result = new ShellResult
            {
                Command = fullCommandForUser,
                Stdout = string.Empty,
                Stderr = ex.Message,
                ExitCode = -2,
                Duration = TimeSpan.Zero
            };

            ExecutionScope.RecordStep(
                Translations.Service_Shell_Name,
                fullCommandForUser,
                false,
                revertStep,
                ex.Message,
                async () => policy.IsSuccess(await RunAsync(fileName, arguments, command, serviceName, revertStep, policy, ct)));

            return result;
        }
    }

    #region Command Prompt methods

    /// <summary>
    ///     Runs a command in the Command Prompt (cmd.exe).
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="revertStep">The revert step to record.</param>
    /// <param name="policy">The policy to use for determining success.</param>
    /// <returns>The result of the command execution.</returns>
    public static ShellResult CMD(string command, ShellRevertStep? revertStep = null, ShellPolicy? policy = null)
    {
        return Run("cmd.exe", "/c", command, nameof(CMD), revertStep, policy);
    }

    /// <summary>
    ///     Runs a command in the Command Prompt (cmd.exe) asynchronously.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="revertStep">The revert step to record.</param>
    /// <param name="policy">The policy to use for determining success.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The result of the command execution.</returns>
    public static Task<ShellResult> CMDAsync(
        string command,
        ShellRevertStep? revertStep = null,
        ShellPolicy? policy = null,
        CancellationToken ct = default)
    {
        return RunAsync(
            "cmd.exe",
            "/c",
            command,
            nameof(CMD),
            revertStep,
            policy,
            ct);
    }

    /// <summary>
    ///     Runs a command in the Command Prompt (cmd.exe) with a specific revert command.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="revertCommand">The command to execute for reverting.</param>
    /// <param name="policy">The policy to use for determining success.</param>
    /// <returns>The result of the command execution.</returns>
    public static ShellResult CMD(string command, string revertCommand, ShellPolicy? policy = null)
    {
        return CMD(command, new ShellRevertStep
        {
            ShellType = ShellType.CMD,
            Command = revertCommand
        }, policy);
    }

    /// <summary>
    ///     Runs a command in the Command Prompt (cmd.exe) with a specific revert command.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="revertCommand">The command to execute for reverting.</param>
    /// <param name="policy">The policy to use for determining success.</param>
    /// <returns>The result of the command execution.</returns>
    public static ShellResult CMD(string command, Func<string> revertCommand, ShellPolicy? policy = null)
    {
        return CMD(command, new ShellRevertStep
        {
            ShellType = ShellType.CMD,
            Command = revertCommand()
        }, policy);
    }

    #endregion Command Prompt methods

    #region PowerShell methods

    /// <summary>
    ///     Runs a command in PowerShell.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="revertStep">The revert step to record.</param>
    /// <param name="policy">The policy to use for determining success.</param>
    /// <returns>The result of the command execution.</returns>
    public static ShellResult PowerShell(string command, ShellRevertStep? revertStep = null, ShellPolicy? policy = null)
    {
        command =
            "$ProgressPreference='SilentlyContinue'; " +
            "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; " +
            "$OutputEncoding = [System.Console]::OutputEncoding = [System.Console]::InputEncoding = [System.Text.Encoding]::UTF8; " +
            command;
        return Run("powershell.exe",
            "-NonInteractive -NoLogo -NoProfile -ExecutionPolicy Bypass -EncodedCommand",
            command.EncodeBase64(),
            nameof(PowerShell),
            revertStep,
            policy);
    }

    /// <summary>
    ///     Runs a command in PowerShell asynchronously.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="revertStep">The revert step to record.</param>
    /// <param name="policy">The policy to use for determining success.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The result of the command execution.</returns>
    public static Task<ShellResult> PowerShellAsync(
        string command,
        ShellRevertStep? revertStep = null,
        ShellPolicy? policy = null,
        CancellationToken ct = default)
    {
        command =
            "$ProgressPreference='SilentlyContinue'; " +
            "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; " +
            "$OutputEncoding = [System.Console]::OutputEncoding = [System.Console]::InputEncoding = [System.Text.Encoding]::UTF8; " +
            command;
        return RunAsync("powershell.exe",
            "-NonInteractive -NoLogo -NoProfile -ExecutionPolicy Bypass -EncodedCommand",
            command.EncodeBase64(),
            nameof(PowerShell),
            revertStep,
            policy,
            ct);
    }

    /// <summary>
    ///     Runs a command in PowerShell with a specific revert command.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="revertCommand">The command to execute for reverting.</param>
    /// <param name="policy">The policy to use for determining success.</param>
    /// <returns>The result of the command execution.</returns>
    public static ShellResult PowerShell(string command, string revertCommand, ShellPolicy? policy = null)
    {
        return PowerShell(command, new ShellRevertStep
        {
            ShellType = ShellType.PowerShell,
            Command = revertCommand
        }, policy);
    }

    /// <summary>
    ///     Runs a command in PowerShell with a specific revert command.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="revertCommand">The command to execute for reverting.</param>
    /// <param name="policy">The policy to use for determining success.</param>
    /// <returns>The result of the command execution.</returns>
    public static ShellResult PowerShell(string command, Func<string> revertCommand, ShellPolicy? policy = null)
    {
        return PowerShell(command, new ShellRevertStep
        {
            ShellType = ShellType.PowerShell,
            Command = revertCommand()
        }, policy);
    }

    #endregion PowerShell methods
}