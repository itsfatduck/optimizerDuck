using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.Revert.Steps;

namespace optimizerDuck.Services.System.Primitives;

public static class ServiceProcessService
{
    private const int ErrorServiceDoesNotExist = 1060;

    private const int ErrorAccessDenied = 5;

    /// <summary>
    ///     Retrieves the current startup type of a Windows service from the Service Control Manager.
    /// </summary>
    /// <param name="serviceName">The internal service name.</param>
    /// <param name="logger">Optional logger used only for logging.</param>
    /// <returns>
    /// A tuple. <c>StartupType</c> is the type Windows reports (null for boot and system starts,
    /// which have no <see cref="ServiceStartupType"/> value). <c>NotFound</c> is
    /// <see langword="true"/> when the service does not exist
    /// (<c>ERROR_SERVICE_DOES_NOT_EXIST</c>, 1060), <see langword="false"/> for other errors.
    /// </returns>
    public static Task<(ServiceStartupType? StartupType, bool NotFound)> GetStartupTypeAsync(
        string serviceName,
        ILogger? logger = null
    )
    {
        var (startupType, error) = QueryStartupType(serviceName);

        if (error != 0 && error != ErrorServiceDoesNotExist)
            logger?.LogWarning(
                "[SERVICE][{Name}] opening the service failed with Win32 error {Error}",
                serviceName,
                error
            );

        return Task.FromResult((startupType, error == ErrorServiceDoesNotExist));
    }

    /// <summary>
    ///     Changes the startup type of a single Windows service through the Service Control Manager.
    ///     Records the change into <paramref name="call"/>.
    /// </summary>
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
                call.Changes.AddSkip(ServiceStrings.ServiceName, skipDescription);
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
                call.Changes.AddSkip(ServiceStrings.ServiceName, alreadyDescription);
                return MapToOpResult(ServiceChangeResult.AlreadyConfigured, null, null, null);
            }

            var write = SetStartupType(item.Name, item.StartupType);
            var nativeError = write.Error;

            var success = nativeError == 0;
            sw.Stop();

            string? errorDetail = null;
            if (!success)
                errorDetail = BuildWriteErrorDetail(write.StartTypeWritten, nativeError);

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

            if (nativeError == ErrorAccessDenied)
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
                call.Changes.AddSkip(ServiceStrings.ServiceName, accessDeniedError);
                return MapToOpResult(
                    ServiceChangeResult.AccessDenied,
                    null,
                    accessDeniedError,
                    errorDetail
                );
            }

            // A delayed auto start flag that Windows refuses still leaves the start type
            // written, so the recorded step carries the previous one and a revert can put it
            // back even though the outcome is a failure.
            ServiceRevertStep? partialRevert = null;
            if (write.StartTypeWritten)
                partialRevert = new ServiceRevertStep
                {
                    ServiceName = item.Name,
                    OriginalStartupType = originalStartupType.Value,
                };

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
                partialRevert,
                error,
                errorDetail,
                retryCall => ChangeServiceStartupTypeAsync(retryCall, item)
            );
            return MapToOpResult(ServiceChangeResult.Failed, partialRevert, error, errorDetail);
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
    ///     Success, NotFound, AlreadyConfigured and AccessDenied are informational outcomes,
    ///     because a refusal by Windows is a skip rather than a failure; only Failed carries an
    ///     error naming the service.
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
            or ServiceChangeResult.AlreadyConfigured
            or ServiceChangeResult.AccessDenied => OpResult.Success(revert),
            _ => OpResult.Fail(
                error ?? ServiceStrings.ServiceErrorChangeStartupTypeFailed,
                errorDetail
            ),
        };
    }

    // =============================================
    // Service Control Manager interop
    // =============================================

    private const int ScManagerConnect = 0x0001;
    private const int ServiceQueryConfig = 0x0001;
    private const int ServiceChangeConfig = 0x0002;

    private const uint ScServiceNoChange = 0xFFFFFFFF;
    private const uint ScServiceAutoStart = 0x00000002;
    private const uint ScServiceDemandStart = 0x00000003;
    private const uint ScServiceDisabled = 0x00000004;

    private const uint ServiceConfigDelayedAutoStartInfo = 3;

    /// <summary>Offset of <c>dwStartType</c> in the native <c>QUERY_SERVICE_CONFIG</c> structure.</summary>
    private const int QueryServiceConfigStartTypeOffset = 4;

    /// <summary>
    ///     Comfortably larger than <c>QUERY_SERVICE_CONFIG</c> plus the variable-length strings
    ///     Windows appends to the same buffer; only the fixed prefix is read.
    /// </summary>
    private const int QueryServiceConfigBufferSize = 8 * 1024;

    /// <summary>
    ///     Reads the configured start type, and whether the delayed flag is set, through the SCM.
    ///     The returned error is a Win32 code (0 on success).
    /// </summary>
    private static (ServiceStartupType? StartupType, int Error) QueryStartupType(string serviceName)
    {
        var manager = OpenSCManager(null, null, ScManagerConnect);
        if (manager == IntPtr.Zero)
            return (null, Marshal.GetLastWin32Error());

        try
        {
            var service = OpenService(manager, serviceName, ServiceQueryConfig);
            if (service == IntPtr.Zero)
                return (null, Marshal.GetLastWin32Error());

            try
            {
                var buffer = Marshal.AllocHGlobal(QueryServiceConfigBufferSize);
                try
                {
                    if (!QueryServiceConfig(service, buffer, QueryServiceConfigBufferSize, out _))
                        return (null, Marshal.GetLastWin32Error());

                    var startType = (uint)
                        Marshal.ReadInt32(buffer, QueryServiceConfigStartTypeOffset);

                    return (
                        startType switch
                        {
                            ScServiceAutoStart => IsDelayedAutoStart(service)
                                ? ServiceStartupType.AutomaticDelayedStart
                                : ServiceStartupType.Automatic,
                            ScServiceDemandStart => ServiceStartupType.Manual,
                            ScServiceDisabled => ServiceStartupType.Disabled,
                            // Boot (0) and system (1) starts have no ServiceStartupType value.
                            _ => null,
                        },
                        0
                    );
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(manager);
        }
    }

    private static bool IsDelayedAutoStart(IntPtr service)
    {
        var buffer = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            // A missing record means the flag is not set, not a failure.
            return QueryServiceConfig2(
                    service,
                    ServiceConfigDelayedAutoStartInfo,
                    buffer,
                    sizeof(int),
                    out _
                )
                && Marshal.ReadInt32(buffer) != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    ///     Outcome of a start type write: the Win32 error, and whether the start type itself was
    ///     written before a later call was refused.
    /// </summary>
    private readonly record struct StartTypeWrite(int Error, bool StartTypeWritten);

    /// <summary>
    ///     Describes which call failed, so a partial write is never mistaken for "nothing ran".
    /// </summary>
    internal static string BuildWriteErrorDetail(bool startTypeWritten, int nativeError) =>
        startTypeWritten
            ? $"ChangeServiceConfig2 (delayed auto start) failed with Win32 error {nativeError}, after the start type was written"
            : $"ChangeServiceConfig failed with Win32 error {nativeError}";

    /// <summary>
    ///     Writes the start type, then the delayed flag for auto-start targets. Returns the Win32
    ///     error and whether the start type was written before the refusal.
    /// </summary>
    private static StartTypeWrite SetStartupType(string serviceName, ServiceStartupType startupType)
    {
        var desiredStart = startupType switch
        {
            ServiceStartupType.Automatic or ServiceStartupType.AutomaticDelayedStart =>
                ScServiceAutoStart,
            ServiceStartupType.Manual => ScServiceDemandStart,
            ServiceStartupType.Disabled => ScServiceDisabled,
            _ => ScServiceDemandStart,
        };

        var manager = OpenSCManager(null, null, ScManagerConnect);
        if (manager == IntPtr.Zero)
            return new StartTypeWrite(Marshal.GetLastWin32Error(), false);

        try
        {
            var service = OpenService(
                manager,
                serviceName,
                ServiceQueryConfig | ServiceChangeConfig
            );
            if (service == IntPtr.Zero)
                return new StartTypeWrite(Marshal.GetLastWin32Error(), false);

            try
            {
                if (
                    !ChangeServiceConfig(
                        service,
                        ScServiceNoChange,
                        desiredStart,
                        ScServiceNoChange,
                        null,
                        null,
                        IntPtr.Zero,
                        null,
                        null,
                        null,
                        null
                    )
                )
                    return new StartTypeWrite(Marshal.GetLastWin32Error(), false);

                // The flag is ignored for non-auto-start services, and leaving a previous "delayed"
                // flag behind would keep reporting the old state, so it is always written for
                // auto-start targets.
                if (desiredStart == ScServiceAutoStart)
                {
                    var info = new SERVICE_DELAYED_AUTO_START_INFO
                    {
                        fDelayedAutostart = startupType == ServiceStartupType.AutomaticDelayedStart,
                    };

                    if (!ChangeServiceConfig2(service, ServiceConfigDelayedAutoStartInfo, ref info))
                        // The start type is already written at this point.
                        return new StartTypeWrite(Marshal.GetLastWin32Error(), true);
                }

                return new StartTypeWrite(0, true);
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(manager);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SERVICE_DELAYED_AUTO_START_INFO
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool fDelayedAutostart;
    }

    [DllImport(
        "advapi32.dll",
        EntryPoint = "OpenSCManagerW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode,
        SetLastError = true
    )]
    private static extern IntPtr OpenSCManager(
        string? machineName,
        string? databaseName,
        int desiredAccess
    );

    [DllImport(
        "advapi32.dll",
        EntryPoint = "OpenServiceW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode,
        SetLastError = true
    )]
    private static extern IntPtr OpenService(
        IntPtr scManager,
        string serviceName,
        int desiredAccess
    );

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceConfig(
        IntPtr service,
        IntPtr queryServiceConfig,
        int bufferSize,
        out int bytesNeeded
    );

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceConfig2(
        IntPtr service,
        uint infoLevel,
        IntPtr buffer,
        int bufferSize,
        out int bytesNeeded
    );

    [DllImport(
        "advapi32.dll",
        EntryPoint = "ChangeServiceConfigW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode,
        SetLastError = true
    )]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeServiceConfig(
        IntPtr service,
        uint serviceType,
        uint startType,
        uint errorControl,
        string? binaryPathName,
        string? loadOrderGroup,
        IntPtr tagId,
        string? dependencies,
        string? serviceStartName,
        string? password,
        string? displayName
    );

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeServiceConfig2(
        IntPtr service,
        uint infoLevel,
        ref SERVICE_DELAYED_AUTO_START_INFO info
    );

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr serviceHandle);
}
