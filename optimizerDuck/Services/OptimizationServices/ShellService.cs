using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Core.Models.Config;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Core.Models.Revert.Steps;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Managers;

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

    private static ShellResult Run(
        string fileName,
        string arguments,
        string command,
        string serviceName,
        ShellRevertStep? revertStep,
        ShellPolicy? policy = null)
    {
        policy ??= ShellPolicy.Default;

        var commandForUser = arguments.Contains("-EncodedCommand", StringComparison.OrdinalIgnoreCase)
            ? command.DecodeBase64().Replace("$ProgressPreference='SilentlyContinue'; ", "")
            : command;

        var fullCommandForUser =
            $"{fileName} {arguments.Replace("-EncodedCommand", "-Command", StringComparison.OrdinalIgnoreCase)} {commandForUser}";

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
                    ServiceTracker.LogError(null, "Failed to kill process for timeout");
                }

                process.WaitForExit(2000); // grace drain
            }

            sw.Stop();
            var duration = sw.Elapsed;
            var stdout = stdoutBuilder.ToString().Trim();
            var stderr = stderrBuilder.ToString().ParseCliXml().Trim();

            var result = new ShellResult(
                fullCommandForUser,
                stdout,
                stderr,
                timedOut ? -1 : process.ExitCode, // use -1 error code for timed out
                duration);

            var success = policy.IsSuccess(result);

            ServiceTracker.Track(serviceName, success);

            ServiceTracker.LogInfo("[{Service}][{Status}][EC={ExitCode}][D={Duration}] {Command}",
                serviceName,
                timedOut ? "TIMEOUT" : success ? "OK" : "FAIL",
                result.ExitCode,
                duration.FormatTime(),
                fullCommandForUser);

            ServiceTracker.LogTrace("[{Service}][STDOUT] {Stdout}",
                serviceName,
                string.IsNullOrWhiteSpace(stdout) ? "N/A" : stdout
            );
            ServiceTracker.LogTrace("[{Service}][STDERR] {Stderr}",
                serviceName,
                string.IsNullOrWhiteSpace(stderr) ? "N/A" : stderr
            );

            var error = success ? null : policy.ErrorFactory(result);

            ServiceTracker.TrackStep(
                "Shell",
                fullCommandForUser,
                success,
                error,
                () => Task.Run(() =>
                    policy.IsSuccess(Run(fileName, arguments, command, serviceName, revertStep, policy))
                )
            );

            if (success && revertStep is not null)
                RevertManager.Record(revertStep);

            return result;
        }
        catch (Exception ex)
        {
            var result = new ShellResult(
                fullCommandForUser,
                string.Empty,
                ex.Message,
                -2,
                TimeSpan.Zero);

            ServiceTracker.Track(serviceName, false);
            ServiceTracker.LogError(ex, "[{Service}][FAIL][EXCEPTION] {Command}", serviceName, fullCommandForUser);
            ServiceTracker.TrackStep(
                "Shell",
                fullCommandForUser,
                false,
                ex.Message,
                () => Task.FromResult(false));

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

        var commandForUser = arguments.Contains("-EncodedCommand", StringComparison.OrdinalIgnoreCase)
            ? command.DecodeBase64().Replace("$ProgressPreference='SilentlyContinue'; ", "")
            : command;

        var fullCommandForUser =
            $"{fileName} {arguments.Replace("-EncodedCommand", "-Command", StringComparison.OrdinalIgnoreCase)} {commandForUser}";

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

            var result = new ShellResult(
                fullCommandForUser,
                stdout,
                stderr,
                timedOut ? -1 : process.ExitCode,
                sw.Elapsed);

            var success = policy.IsSuccess(result);

            ServiceTracker.Track(serviceName, success);

            ServiceTracker.LogInfo("[{Service}][{Status}][EC={ExitCode}][D={Duration}] {Command}",
                serviceName,
                timedOut ? "TIMEOUT" : success ? "OK" : "FAIL",
                result.ExitCode,
                sw.Elapsed.FormatTime(),
                fullCommandForUser);

            ServiceTracker.LogTrace("[{Service}][STDOUT] {Stdout}",
                serviceName,
                string.IsNullOrWhiteSpace(stdout) ? "N/A" : stdout);

            ServiceTracker.LogTrace("[{Service}][STDERR] {Stderr}",
                serviceName,
                string.IsNullOrWhiteSpace(stderr) ? "N/A" : stderr);

            var error = success ? null : policy.ErrorFactory(result);

            ServiceTracker.TrackStep(
                "Shell",
                fullCommandForUser,
                success,
                error,
                async () =>
                    policy.IsSuccess(await RunAsync(fileName, arguments, command, serviceName, revertStep, policy, ct))
            );

            if (success && revertStep is not null)
                RevertManager.Record(revertStep);

            return result;
        }
        catch (Exception ex)
        {
            var result = new ShellResult(
                fullCommandForUser,
                string.Empty,
                ex.Message,
                -2,
                TimeSpan.Zero);

            ServiceTracker.Track(serviceName, false);
            ServiceTracker.LogError(ex, "[{Service}][FAIL][EXCEPTION] {Command}", serviceName, fullCommandForUser);

            ServiceTracker.TrackStep(
                "Shell",
                fullCommandForUser,
                false,
                ex.Message,
                () => Task.FromResult(false));

            return result;
        }
    }


    #region Command Prompt methods

    public static ShellResult CMD(string command, ShellRevertStep? revertStep = null, ShellPolicy? policy = null)
    {
        return Run("cmd.exe", "/c", command, nameof(CMD), revertStep, policy);
    }

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


    public static ShellResult CMD(string command, string revertCommand, ShellPolicy? policy = null)
    {
        return CMD(command, new ShellRevertStep
        {
            ShellType = ShellType.CMD,
            Command = revertCommand
        }, policy);
    }

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

    public static ShellResult PowerShell(string command, ShellRevertStep? revertStep = null, ShellPolicy? policy = null)
    {
        command = "$ProgressPreference='SilentlyContinue'; " + command;
        return Run("powershell.exe",
            "-NonInteractive -NoLogo -NoProfile -ExecutionPolicy Bypass -EncodedCommand",
            command.EncodeBase64(),
            nameof(PowerShell),
            revertStep,
            policy);
    }

    public static Task<ShellResult> PowerShellAsync(
        string command,
        ShellRevertStep? revertStep = null,
        ShellPolicy? policy = null,
        CancellationToken ct = default)
    {
        command = "$ProgressPreference='SilentlyContinue'; " + command;
        return RunAsync("powershell.exe",
            "-NonInteractive -NoLogo -NoProfile -ExecutionPolicy Bypass -EncodedCommand",
            command.EncodeBase64(),
            nameof(PowerShell),
            revertStep,
            policy, ct);
    }

    public static ShellResult PowerShell(string command, string revertCommand, ShellPolicy? policy = null)
    {
        return PowerShell(command, new ShellRevertStep
        {
            ShellType = ShellType.PowerShell,
            Command = revertCommand
        }, policy);
    }

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