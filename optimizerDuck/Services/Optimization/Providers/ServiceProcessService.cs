using System.Diagnostics;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.Revert.Steps;

namespace optimizerDuck.Services.Optimization.Providers;

public static class ServiceProcessService
{
    private const int ErrorServiceDoesNotExist = 1060;

    private const int ErrorAccessDenied = 5;

    private const int DefaultScTimeoutMs = 30000;
    private const int DefaultScQueryTimeoutMs = 15000;

    /// <summary>
    ///     Parses the START_TYPE from raw <c>sc qc</c> stdout.
    ///     Exposed as internal for unit testing.
    /// </summary>
    internal static (ServiceStartupType? StartupType, bool ParseFailed) ParseScStartType(
        string stdout
    )
    {
        var (type, matched) = Windows.Services.ScStartupTypeParser.ParseWithMatch(stdout);
        return (type, !matched);
    }

    /// <summary>Retrieves the current startup type of a Windows service by running <c>sc.exe qc</c>.</summary>
    /// <param name="serviceName">The internal service name.</param>
    /// <param name="logger">Optional logger used only for logging.</param>
    /// <returns>
    /// A tuple. <c>StartupType</c> is the type if parsed successfully.
    /// <c>NotFound</c> is <see langword="true"/> when the service does not exist (exit code 1060),
    /// <see langword="false"/> for other errors.
    /// </returns>
    public static async Task<(ServiceStartupType? StartupType, bool NotFound)> GetStartupTypeAsync(
        string serviceName,
        ILogger? logger = null
    )
    {
        try
        {
            var (exitCode, stdout, stderr) = await RunScExeAsync(
                $"qc \"{serviceName}\"",
                DefaultScQueryTimeoutMs
            );

            if (exitCode != 0)
            {
                var notFound = exitCode == ErrorServiceDoesNotExist;
                logger?.LogWarning(
                    "[SERVICE][{Name}] sc.exe qc failed with exit code {ExitCode}: {Stderr}",
                    serviceName,
                    exitCode,
                    stderr
                );
                return (null, notFound);
            }

            var (result, parseError) = ParseScStartType(stdout);

            if (parseError)
            {
                logger?.LogWarning(
                    "[SERVICE][{Name}] Could not parse START_TYPE from sc.exe qc output:\n{Output}",
                    serviceName,
                    stdout
                );
                return (null, false);
            }

            return (result, false);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to get startup type for {ServiceName}", serviceName);
            return (null, false);
        }
    }

    /// <summary>Changes the startup type of a single Windows service via <c>sc.exe config</c>. Records the change into <paramref name="call"/>.</summary>
    /// <param name="call">The explicit call context: change collector, logger and cancellation token.</param>
    /// <param name="item">The service item with the target startup type.</param>
    /// <returns>The outcome of the change request.</returns>
    public static async Task<OpResult> ChangeServiceStartupTypeAsync(OpCall call, ServiceItem item)
    {
        ArgumentNullException.ThrowIfNull(call);

        var description = ServiceStrings.Format(
            ServiceStrings.ServiceDescriptionChange,
            item.Name,
            item.StartupType
        );
        var sw = Stopwatch.StartNew();

        try
        {
            var (originalStartupType, notFound) = await GetStartupTypeAsync(item.Name, call.Logger);

            if (notFound)
            {
                sw.Stop();
                var skipDescription = ServiceStrings.Format(
                    ServiceStrings.ServiceInfoSkippedNotFound,
                    item.Name
                );
                call.Logger.LogInformation("[SERVICE][{Name}] not found, skipping", item.Name);
                call.Changes.Add(ServiceStrings.ServiceName, skipDescription, true);
                return MapToOpResult(ServiceChangeResult.NotFound, null, null, null);
            }

            if (originalStartupType == null)
            {
                sw.Stop();
                var queryError = $"Could not query startup type for service '{item.Name}'";
                call.Logger.LogInformation(
                    "[SERVICE][{Name}][FAIL][D={Duration}] could not query startup type",
                    item.Name,
                    sw.Elapsed.FormatTime()
                );
                call.Changes.Add(
                    ServiceStrings.ServiceName,
                    description,
                    false,
                    null,
                    queryError,
                    null,
                    retryCall => ChangeServiceStartupTypeAsync(retryCall, item)
                );
                return MapToOpResult(ServiceChangeResult.Failed, null, queryError, null);
            }

            if (originalStartupType.Value == item.StartupType)
            {
                sw.Stop();
                var alreadyDescription = ServiceStrings.Format(
                    ServiceStrings.ServiceInfoAlreadyConfigured,
                    item.Name,
                    item.StartupType
                );
                call.Logger.LogInformation(
                    "[SERVICE][{Name}] already {StartupType}, skipping",
                    item.Name,
                    item.StartupType
                );
                call.Changes.Add(ServiceStrings.ServiceName, alreadyDescription, true);
                return MapToOpResult(ServiceChangeResult.AlreadyConfigured, null, null, null);
            }

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
                DefaultScTimeoutMs
            );

            var success = exitCode == 0;
            sw.Stop();

            string? errorDetail = null;
            if (!success)
                errorDetail = $"sc.exe exit code: {exitCode}\nstdout: {stdout}\nstderr: {stderr}";

            if (success)
            {
                ServiceRevertStep? revertStep = null;
                if (originalStartupType.Value != item.StartupType)
                    revertStep = new ServiceRevertStep
                    {
                        ServiceName = item.Name,
                        OriginalStartupType = originalStartupType.Value,
                    };

                call.Logger.LogInformation(
                    "[SERVICE][{Name}][OK][D={Duration}] startup -> {StartupType}",
                    item.Name,
                    sw.Elapsed.FormatTime(),
                    item.StartupType
                );

                call.Changes.Add(ServiceStrings.ServiceName, description, true, revertStep);
                return MapToOpResult(ServiceChangeResult.Success, revertStep, null, null);
            }

            if (exitCode == ErrorAccessDenied)
            {
                var accessDeniedError = ServiceStrings.Format(
                    ServiceStrings.ServiceInfoSkippedAccessDenied,
                    item.Name
                );
                call.Logger.LogInformation(
                    "[SERVICE][{Name}][SKIP][D={Duration}] access denied, Windows protects this service",
                    item.Name,
                    sw.Elapsed.FormatTime()
                );
                call.Changes.Add(
                    ServiceStrings.ServiceName,
                    description,
                    false,
                    null,
                    accessDeniedError,
                    errorDetail
                );
                return MapToOpResult(
                    ServiceChangeResult.AccessDenied,
                    null,
                    accessDeniedError,
                    errorDetail
                );
            }

            var error = $"{ServiceStrings.ServiceErrorChangeStartupTypeFailed} '{item.Name}'";
            call.Logger.LogInformation(
                "[SERVICE][{Name}][FAIL][D={Duration}] startup -> {StartupType}",
                item.Name,
                sw.Elapsed.FormatTime(),
                item.StartupType
            );
            call.Changes.Add(
                ServiceStrings.ServiceName,
                description,
                false,
                null,
                error,
                errorDetail,
                retryCall => ChangeServiceStartupTypeAsync(retryCall, item)
            );
            return MapToOpResult(ServiceChangeResult.Failed, null, error, errorDetail);
        }
        catch (Exception ex)
        {
            var exceptionError = ServiceStrings.Format(
                ServiceStrings.ServiceErrorExceptionOccurred,
                item.Name,
                ex.Message
            );
            var exceptionDetail = ex.ToString();

            call.Logger.LogError(
                ex,
                "[SERVICE][{Name}][FAIL][EXCEPTION] startup -> {StartupType}",
                item.Name,
                item.StartupType
            );
            call.Changes.Add(
                ServiceStrings.ServiceName,
                description,
                false,
                null,
                exceptionError,
                exceptionDetail,
                retryCall => ChangeServiceStartupTypeAsync(retryCall, item)
            );
            return MapToOpResult(ServiceChangeResult.Failed, null, exceptionError, exceptionDetail);
        }
    }

    /// <summary>Changes the startup type for multiple services, serially.</summary>
    /// <param name="call">The explicit call context; cancellation is honored between items.</param>
    /// <param name="items">The service items to update.</param>
    public static async Task ChangeServiceStartupTypeAsync(OpCall call, ServiceItem[] items)
    {
        ArgumentNullException.ThrowIfNull(call);

        foreach (var item in items)
        {
            call.CancellationToken.ThrowIfCancellationRequested();
            await ChangeServiceStartupTypeAsync(call, item);
        }
    }

    /// <summary>
    ///     Maps the compat <see cref="ServiceChangeResult"/> outcome to an <see cref="OpResult"/>.
    ///     NotFound and AlreadyConfigured are informational successes; AccessDenied and
    ///     Failed carry an error naming the service.
    /// </summary>
    private static OpResult MapToOpResult(
        ServiceChangeResult outcome,
        IRevertStep? revert,
        string? error,
        string? errorDetail
    )
    {
        return outcome switch
        {
            ServiceChangeResult.Success
            or ServiceChangeResult.NotFound
            or ServiceChangeResult.AlreadyConfigured => OpResult.Success(revert),
            _ => OpResult.Fail(
                error ?? ServiceStrings.ServiceErrorChangeStartupTypeFailed,
                errorDetail
            ),
        };
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunScExeAsync(
        string arguments,
        int timeoutMs
    )
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
            try
            {
                process.Kill();
            }
            catch { }
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return (process.ExitCode, stdout, stderr);
    }
}
