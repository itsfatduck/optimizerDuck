using System.Diagnostics;
using System.Text.RegularExpressions;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Resources.Languages;

namespace optimizerDuck.Services.Optimization.Providers;

public static class ServiceProcessService
{
    private static readonly AsyncLocal<string?> _lastError = new();
    private static readonly AsyncLocal<string?> _lastErrorDetail = new();

    private static readonly Regex _startTypeRegex = new(
        @"START_TYPE\s*:\s*(\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    );

    private static readonly Regex _delayedRegex = new(
        @"\(Delayed\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    );

    internal static string? LastError => _lastError.Value;
    internal static string? LastErrorDetail => _lastErrorDetail.Value;

    public static async Task<ServiceStartupType?> GetStartupTypeAsync(string serviceName)
    {
        try
        {
            var (exitCode, stdout, stderr) = await RunScExeAsync($"qc \"{serviceName}\"", 15000);

            if (exitCode != 0)
            {
                ExecutionScope.LogWarning(
                    "[SERVICE][{Name}] sc.exe qc failed with exit code {ExitCode}: {Stderr}",
                    serviceName,
                    exitCode,
                    stderr
                );
                return null;
            }

            var startMatch = _startTypeRegex.Match(stdout);
            if (!startMatch.Success)
            {
                ExecutionScope.LogWarning(
                    "[SERVICE][{Name}] Could not parse START_TYPE from sc.exe qc output",
                    serviceName
                );
                return null;
            }

            var startValue = int.Parse(startMatch.Groups[1].Value);

            return startValue switch
            {
                2 => _delayedRegex.IsMatch(stdout)
                    ? ServiceStartupType.AutomaticDelayedStart
                    : ServiceStartupType.Automatic,
                3 => ServiceStartupType.Manual,
                4 => ServiceStartupType.Disabled,
                _ => null,
            };
        }
        catch (Exception ex)
        {
            ExecutionScope.LogError(
                ex,
                "Failed to get startup type for {ServiceName}",
                serviceName
            );
            return null;
        }
    }

    public static async Task<bool> ChangeServiceStartupTypeAsync(ServiceItem item)
    {
        _lastError.Value = _lastErrorDetail.Value = null;

        var description = string.Format(
            Translations.Service_Service_Description_Change,
            item.Name,
            item.StartupType
        );
        var sw = Stopwatch.StartNew();

        try
        {
            var originalStartupType = await GetStartupTypeAsync(item.Name);

            var scType = item.StartupType switch
            {
                ServiceStartupType.Automatic => "auto",
                ServiceStartupType.AutomaticDelayedStart => "delayed-auto",
                ServiceStartupType.Manual => "demand",
                ServiceStartupType.Disabled => "disabled",
                _ => "demand",
            };

            var (exitCode, stdout, stderr) = await RunScExeAsync(
                $"config \"{item.Name}\" start= {scType}",
                30000
            );

            var success = exitCode == 0;
            sw.Stop();

            if (!success)
                _lastErrorDetail.Value =
                    $"sc.exe exit code: {exitCode}\nstdout: {stdout}\nstderr: {stderr}";

            if (success)
            {
                ServiceRevertStep? revertStep = null;
                if (originalStartupType.HasValue && originalStartupType.Value != item.StartupType)
                    revertStep = new ServiceRevertStep
                    {
                        ServiceName = item.Name,
                        OriginalStartupType = originalStartupType.Value,
                    };

                ExecutionScope.LogInfo(
                    "[SERVICE][{Name}][OK][D={Duration}] startup -> {StartupType}",
                    item.Name,
                    sw.Elapsed.FormatTime(),
                    item.StartupType
                );

                ExecutionScope.Track(nameof(ChangeServiceStartupTypeAsync), true);
                ExecutionScope.RecordStep(
                    Translations.Service_Service_Name,
                    description,
                    true,
                    revertStep
                );
                return true;
            }

            _lastError.Value =
                Translations.Service_Service_Error_UpdateRegistryForStartupTypeFailed;
            ExecutionScope.LogInfo(
                "[SERVICE][{Name}][FAIL][D={Duration}] startup -> {StartupType}",
                item.Name,
                sw.Elapsed.FormatTime(),
                item.StartupType
            );
            ExecutionScope.Track(nameof(ChangeServiceStartupTypeAsync), false);
            ExecutionScope.RecordStep(
                Translations.Service_Service_Name,
                description,
                false,
                null,
                _lastError.Value,
                () => ChangeServiceStartupTypeAsync(item),
                _lastErrorDetail.Value
            );
            return false;
        }
        catch (Exception ex)
        {
            _lastError.Value = string.Format(
                Translations.Service_Service_Error_ExceptionOccurred,
                item.Name,
                ex.Message
            );
            _lastErrorDetail.Value = ex.ToString();

            ExecutionScope.LogError(
                ex,
                "[SERVICE][{Name}][FAIL][EXCEPTION] startup -> {StartupType}",
                item.Name,
                item.StartupType
            );
            ExecutionScope.Track(nameof(ChangeServiceStartupTypeAsync), false);
            ExecutionScope.RecordStep(
                Translations.Service_Service_Name,
                description,
                false,
                null,
                _lastError.Value,
                () => ChangeServiceStartupTypeAsync(item),
                _lastErrorDetail.Value
            );
            return false;
        }
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunScExeAsync(
        string arguments,
        int timeoutMs)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(); } catch { }
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return (process.ExitCode, stdout, stderr);
    }

    /// <summary>Changes the startup type for multiple services.</summary>
    /// <param name="items">The service items to update.</param>
    public static async Task ChangeServiceStartupTypeAsync(params ServiceItem[] items)
    {
        foreach (var item in items)
            await ChangeServiceStartupTypeAsync(item);
    }
}
