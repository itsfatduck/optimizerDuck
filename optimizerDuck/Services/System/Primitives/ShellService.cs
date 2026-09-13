using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Services.System.Primitives;

/// <summary>
///     Thin instance facade over <see cref="ProcessRunner"/>: builds the
///     cmd.exe / powershell.exe command lines, maps the raw
///     <see cref="ProcessResult"/> to a <see cref="ShellResult"/> for policy
///     evaluation, records a <see cref="Change"/> into the passed
///     <see cref="OpCall"/>, and returns an <see cref="OpResult"/>.
///     Holds no static state; the timeout lives in <see cref="ProcessRunner"/>
///     and is read live from settings on every call.
/// </summary>
public sealed class ShellService
{
    private readonly ProcessRunner _runner;

    public ShellService(ProcessRunner runner)
    {
        _runner = runner;
    }

    private static (string commandForUser, string fullCommandForUser) SanitizeCommandForUser(
        string fileName,
        string arguments,
        string command
    )
    {
        var commandForUser = (
            arguments.Contains("-EncodedCommand", StringComparison.OrdinalIgnoreCase)
                ? command.DecodeBase64()
                : command
        )
            .Replace("$ProgressPreference='SilentlyContinue'; ", "")
            .Replace("[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; ", "")
            .Replace(
                "$OutputEncoding = [System.Console]::OutputEncoding = [System.Console]::InputEncoding = [System.Text.Encoding]::UTF8; ",
                ""
            )
            .Replace("chcp 65001 > nul & ", "");

        var fullCommandForUser =
            $"{fileName} {arguments.Replace("-EncodedCommand", "-Command", StringComparison.OrdinalIgnoreCase)} {commandForUser}";

        return (commandForUser, fullCommandForUser);
    }

    /// <summary>
    ///     Executes the process and maps the raw result to a <see cref="ShellResult"/>
    ///     without recording a <see cref="Change"/>. Shared by the optimization
    ///     path (<see cref="RunCoreAsync"/>) and the raw-run methods below.
    /// </summary>
    private async Task<(ShellResult Result, bool Cancelled)> BuildShellResultAsync(
        string fileName,
        string arguments,
        string command,
        string serviceName,
        ShellPolicy policy,
        ILogger? logger,
        CancellationToken ct
    )
    {
        var (_, fullCommandForUser) = SanitizeCommandForUser(fileName, arguments, command);

        // Best-effort UTF-8 codepage for cmd.exe; PowerShell already forces UTF-8 via its preamble.
        var processArgs = fileName.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase)
            ? ShellMapping.BuildCmdArguments(arguments, command)
            : $"{arguments} {command}";

        var raw = await _runner.RunAsync(fileName, processArgs, ct).ConfigureAwait(false);

        var result = ShellMapping.Map(raw, fullCommandForUser);

        var success = !raw.Cancelled && policy.IsSuccess(result);
        var status =
            raw.Cancelled ? "CANCELLED"
            : raw.TimedOut ? "TIMEOUT"
            : success ? "OK"
            : "FAIL";

        logger?.LogInformation(
            "[{Service}][{Status}][EC={ExitCode}][D={Duration}] {Command}",
            serviceName,
            status,
            result.ExitCode,
            result.Duration.FormatTime(),
            fullCommandForUser
        );

        logger?.LogTrace(
            "[{Service}][STDOUT] {Stdout}",
            serviceName,
            string.IsNullOrWhiteSpace(result.Stdout) ? "N/A" : result.Stdout
        );
        logger?.LogTrace(
            "[{Service}][STDERR] {Stderr}",
            serviceName,
            string.IsNullOrWhiteSpace(result.Stderr) ? "N/A" : result.Stderr
        );

        return (result, raw.Cancelled);
    }

    private async Task<OpResult> RunCoreAsync(
        string fileName,
        string arguments,
        string command,
        string serviceName,
        ShellType shellType,
        IRevertStep? revertStep,
        ShellPolicy? policy,
        OpCall call,
        CancellationToken ct
    )
    {
        policy ??= ShellPolicy.Default;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            call.CancellationToken,
            ct
        );

        var (result, cancelled) = await BuildShellResultAsync(
                fileName,
                arguments,
                command,
                serviceName,
                policy,
                call.Logger,
                linkedCts.Token
            )
            .ConfigureAwait(false);

        var success = !cancelled && policy.IsSuccess(result);

        var error =
            success ? null
            : cancelled ? Loc.Instance["Common.OperationCancelled"]
            : policy.ErrorFactory(result);
        var errorDetail = success ? null : GetShellErrorDetail(result);

        call.Changes.Add(
            ServiceStrings.ShellName,
            result.Command,
            success,
            revertStep,
            error,
            errorDetail,
            success
                ? null
                : retryCall =>
                    RunCoreAsync(
                        fileName,
                        arguments,
                        command,
                        serviceName,
                        shellType,
                        revertStep,
                        policy,
                        retryCall,
                        CancellationToken.None
                    )
        );

        return success ? OpResult.Success(revertStep) : OpResult.Fail(error!, errorDetail);
    }

    #region Raw runs (no Change recording)

    /// <summary>
    ///     Runs a cmd.exe command without recording a <see cref="Change"/>.
    ///     For non-optimization callers (UI services, revert steps, shutdown paths)
    ///     that need the raw <see cref="ShellResult"/> (stdout/stderr/exit code).
    /// </summary>
    public async Task<ShellResult> QueryCMDAsync(
        string command,
        ILogger? logger = null,
        ShellPolicy? policy = null,
        CancellationToken ct = default
    )
    {
        var (result, _) = await BuildShellResultAsync(
                "cmd.exe",
                "/c",
                command,
                nameof(QueryCMDAsync),
                policy ?? ShellPolicy.Default,
                logger,
                ct
            )
            .ConfigureAwait(false);
        return result;
    }

    /// <summary>
    ///     Runs a PowerShell command without recording a <see cref="Change"/>.
    ///     For non-optimization callers (UI services, revert steps, queries)
    ///     that need the raw <see cref="ShellResult"/> (stdout/stderr/exit code).
    /// </summary>
    public async Task<ShellResult> QueryPowerShellAsync(
        string command,
        ILogger? logger = null,
        ShellPolicy? policy = null,
        CancellationToken ct = default
    )
    {
        // -EncodedCommand avoids quoting/escaping issues
        var (result, _) = await BuildShellResultAsync(
                "powershell.exe",
                ShellMapping.PowerShellArguments,
                ShellMapping.EncodePowerShellCommand(command),
                nameof(QueryPowerShellAsync),
                policy ?? ShellPolicy.Default,
                logger,
                ct
            )
            .ConfigureAwait(false);
        return result;
    }

    #endregion Raw runs (no Change recording)

    #region Command Prompt methods


    /// <summary>
    ///     Runs a command in the Command Prompt (cmd.exe) asynchronously.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="call">The explicit per-operation call context (change collector, logger, cancellation).</param>
    /// <param name="revertStep">The revert step to record.</param>
    /// <param name="policy">The policy to use for determining success.</param>
    /// <param name="ct">An additional cancellation token, linked with <see cref="OpCall.CancellationToken"/>.</param>
    /// <returns>The result of the command execution.</returns>
    public Task<OpResult> CMDAsync(
        string command,
        OpCall call,
        IRevertStep? revertStep = null,
        ShellPolicy? policy = null,
        CancellationToken ct = default
    )
    {
        return RunCoreAsync(
            "cmd.exe",
            "/c",
            command,
            "CMD",
            ShellType.CMD,
            revertStep,
            policy,
            call,
            ct
        );
    }

    /// <summary>
    ///     Runs a command in the Command Prompt (cmd.exe) asynchronously with a specific revert command.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="call">The explicit per-operation call context (change collector, logger, cancellation).</param>
    /// <param name="revertCommand">The command to execute for reverting.</param>
    /// <param name="policy">The policy to use for determining success.</param>
    /// <param name="ct">An additional cancellation token, linked with <see cref="OpCall.CancellationToken"/>.</param>
    /// <returns>The result of the command execution.</returns>
    public Task<OpResult> CMDAsync(
        string command,
        OpCall call,
        string revertCommand,
        ShellPolicy? policy = null,
        CancellationToken ct = default
    )
    {
        return RunCoreAsync(
            "cmd.exe",
            "/c",
            command,
            "CMD",
            ShellType.CMD,
            new ShellRevertStep { ShellType = ShellType.CMD, Command = revertCommand },
            policy,
            call,
            ct
        );
    }

    #endregion Command Prompt methods

    #region PowerShell methods


    /// <summary>
    ///     Runs a command in PowerShell asynchronously.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="call">The explicit per-operation call context (change collector, logger, cancellation).</param>
    /// <param name="revertStep">The revert step to record.</param>
    /// <param name="policy">The policy to use for determining success.</param>
    /// <param name="ct">An additional cancellation token, linked with <see cref="OpCall.CancellationToken"/>.</param>
    /// <returns>The result of the command execution.</returns>
    public Task<OpResult> PowerShellAsync(
        string command,
        OpCall call,
        IRevertStep? revertStep = null,
        ShellPolicy? policy = null,
        CancellationToken ct = default
    )
    {
        return RunCoreAsync(
            "powershell.exe",
            ShellMapping.PowerShellArguments,
            ShellMapping.EncodePowerShellCommand(command),
            "PowerShell",
            ShellType.PowerShell,
            revertStep,
            policy,
            call,
            ct
        );
    }

    /// <summary>Runs a command in PowerShell asynchronously with a specific revert command.</summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="call">The explicit per-operation call context (change collector, logger, cancellation).</param>
    /// <param name="revertCommand">The command to execute for reverting.</param>
    /// <param name="policy">The policy for determining success.</param>
    /// <param name="ct">An additional cancellation token, linked with <see cref="OpCall.CancellationToken"/>.</param>
    /// <returns>The result of the command execution.</returns>
    public Task<OpResult> PowerShellAsync(
        string command,
        OpCall call,
        string revertCommand,
        ShellPolicy? policy = null,
        CancellationToken ct = default
    )
    {
        return RunCoreAsync(
            "powershell.exe",
            ShellMapping.PowerShellArguments,
            ShellMapping.EncodePowerShellCommand(command),
            "PowerShell",
            ShellType.PowerShell,
            new ShellRevertStep { ShellType = ShellType.PowerShell, Command = revertCommand },
            policy,
            call,
            ct
        );
    }

    private static string GetShellErrorDetail(ShellResult result)
    {
        var sections = new List<string>
        {
            $"Command: {result.Command}",
            $"Exit Code: {result.ExitCode}",
            $"Duration: {result.Duration.TotalMilliseconds:F0}ms",
        };

        var details = new List<string>();

        if (!string.IsNullOrWhiteSpace(result.Stdout))
        {
            details.Add($"STDOUT:\n{result.Stdout.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(result.Stderr))
        {
            details.Add($"STDERR:\n{result.Stderr.Trim()}");
        }

        if (details.Count > 0)
        {
            sections.Add(string.Empty);
            sections.AddRange(details);
        }

        return string.Join("\n", sections);
    }

    #endregion PowerShell methods
}
