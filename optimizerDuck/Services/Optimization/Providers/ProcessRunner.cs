using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using optimizerDuck.Domain.Configuration;

namespace optimizerDuck.Services.Optimization.Providers;

/// <summary>
///     The captured output of a single child-process execution.
/// </summary>
/// <param name="ExitCode">The process exit code; <c>-1</c> on timeout, <c>-2</c> on exception.</param>
/// <param name="Stdout">Raw captured standard output (not CLIXML-parsed).</param>
/// <param name="Stderr">Raw captured standard error (not CLIXML-parsed).</param>
/// <param name="TimedOut">Whether the process was killed because the shell timeout elapsed.</param>
/// <param name="Duration">Wall-clock time from process start to exit (or kill).</param>
/// <param name="Cancelled">Whether an external <see cref="CancellationToken"/> fired first.</param>
public sealed record ProcessResult(
    int ExitCode,
    string Stdout,
    string Stderr,
    bool TimedOut,
    TimeSpan Duration,
    bool Cancelled = false
);

/// <summary>
///     Single home for child-process execution: unified start, capture,
///     timeout, kill-grace (2s) and force-kill logic. Async only.
///     The timeout is read live from <see cref="AppSettings.Optimize"/>
///     on every call, so settings changes apply without re-construction.
/// </summary>
public sealed class ProcessRunner
{
    private const int GraceDrainMs = 2000;
    private const int ForceKillWaitMs = 5000;

    private readonly Func<int> _readTimeoutMs;
    private readonly ILogger<ProcessRunner>? _logger;

    public ProcessRunner(
        IOptionsMonitor<AppSettings> options,
        ILogger<ProcessRunner>? logger = null
    )
    {
        _readTimeoutMs = () => options.CurrentValue.Optimize.ShellTimeoutMs;
        _logger = logger;
    }

    /// <summary>
    ///     Fixed-timeout construction for call sites outside DI (domain base
    ///     classes, revert steps). App paths use the options overload so
    ///     settings changes apply live.
    /// </summary>
    public ProcessRunner(int timeoutMs, ILogger<ProcessRunner>? logger = null)
    {
        _readTimeoutMs = () => timeoutMs;
        _logger = logger;
    }

    /// <summary>Starts a process, captures stdout/stderr, and enforces the live shell timeout.</summary>
    public async Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        CancellationToken ct = default
    )
    {
        var timeoutMs = Math.Max(1, _readTimeoutMs());

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var sw = Stopwatch.StartNew();
        try
        {
            using var process = new Process { StartInfo = psi };

            var stdoutBuilder = new StringBuilder();
            var stderrBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    stdoutBuilder.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    stderrBuilder.AppendLine(e.Data);
            };

            process.Start();
            var pid = TryGetPid(process);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                timeoutCts.Token,
                ct
            );

            var timedOut = false;
            var cancelled = false;
            try
            {
                await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // External cancellation wins over our own timeout when both fired.
                cancelled = ct.IsCancellationRequested && !timeoutCts.IsCancellationRequested;
                timedOut = !cancelled;

                _logger?.LogWarning(
                    "Process {Reason}, attempting to kill: PID {ProcessId}",
                    cancelled ? "cancelled" : "timed out",
                    pid
                );

                TryKill(process, pid, cancelled ? "cancel" : "timeout");
            }

            sw.Stop();

            if (!HasExited(process))
            {
                // Still running after the kill attempt: grace period, then force kill.
                _logger?.LogWarning(
                    "Process still running after kill, waiting grace period: PID {ProcessId}",
                    pid
                );
                try
                {
                    process.WaitForExit(GraceDrainMs);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(
                        ex,
                        "Failed to wait for process exit after kill, PID: {ProcessId}",
                        pid
                    );
                }

                if (!HasExited(process))
                {
                    _logger?.LogWarning(
                        "Process still running after grace period, force killing: PID {ProcessId}",
                        pid
                    );
                    TryKill(process, pid, "force kill");
                    try
                    {
                        if (!process.WaitForExit(ForceKillWaitMs))
                            _logger?.LogWarning(
                                "Process did not exit after force kill: PID {ProcessId}. User may need manual cleanup.",
                                pid
                            );
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(
                            ex,
                            "Failed to wait for process exit after force kill, PID: {ProcessId}",
                            pid
                        );
                    }
                }

                timedOut |= !cancelled && !HasExited(process);
            }

            var exited = HasExited(process);
            return new ProcessResult(
                !exited ? -1
                    : timedOut ? -1
                    : process.ExitCode,
                stdoutBuilder.ToString().Trim(),
                stderrBuilder.ToString().Trim(),
                timedOut || !exited,
                sw.Elapsed,
                cancelled
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger?.LogError(ex, "Failed to execute {FileName}", fileName);
            return new ProcessResult(
                -2,
                string.Empty,
                ex.Message,
                false,
                sw.Elapsed,
                ct.IsCancellationRequested
            );
        }
    }

    private void TryKill(Process process, int pid, string reason)
    {
        try
        {
            if (!HasExited(process))
            {
                process.Kill(entireProcessTree: true);
                _logger?.LogInformation("Killed process ({Reason}): PID {ProcessId}", reason, pid);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Failed to kill process ({Reason}), PID: {ProcessId}",
                reason,
                pid
            );
        }
    }

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch
        {
            return true;
        }
    }

    private static int TryGetPid(Process process)
    {
        try
        {
            return process.Id;
        }
        catch
        {
            return -1;
        }
    }
}
