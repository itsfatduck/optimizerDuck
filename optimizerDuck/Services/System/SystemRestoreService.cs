using System.Management;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Services.System;

/// <summary>
///     System Restore over the documented <c>SystemRestore</c> class in <c>root\default</c> - the
///     same subsystem that backs <c>Checkpoint-Computer</c> and
///     <c>Enable-ComputerRestore</c>, without a child process. Single owner of every System
///     Restore WMI call in the app.
///     Reads take ids only; mutating calls take an optional <see cref="ILogger" /> (default null),
///     return typed results carrying the real native status, and record nothing - so a
///     restore-point manager tool can reuse this service exactly like a tool reuses
///     <see cref="PowerPlanService" />. Virtual members are the hand-double seam for tests, because
///     System Restore needs elevation and test hosts do not have it.
/// </summary>
[SupportedOSPlatform("windows")]
public class SystemRestoreService
{
    private const string NamespacePath = @"root\default";
    private const string ClassName = "SystemRestore";

    private const string FrequencyKey =
        @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";

    private const string FrequencyValueName = "SystemRestorePointCreationFrequency";

    /// <summary>RestorePointType MODIFY_SETTINGS.</summary>
    public const uint RestorePointTypeModifySettings = 12;

    /// <summary>EventType BEGIN_SYSTEM_CHANGE.</summary>
    public const uint EventTypeBeginSystemChange = 100;

    /// <summary>ERROR_SERVICE_DISABLED: System Restore is not enabled for the drive.</summary>
    public const uint ErrorServiceDisabled = 1058;

    /// <summary>
    ///     HRESULT_FROM_WIN32(ERROR_SERVICE_DISABLED), the other shape that refusal takes.
    /// </summary>
    public const uint HrServiceDisabled = 0x80070422;

    /// <summary>
    ///     Documented default for <c>SystemRestorePointCreationFrequency</c> when the value is
    ///     absent: 24 hours.
    /// </summary>
    public const int DefaultCreationFrequencyMinutes = 24 * 60;

    private readonly ILogger<SystemRestoreService> _logger;

    public SystemRestoreService(ILogger<SystemRestoreService> logger) => _logger = logger;

    /// <summary>One restore point as Windows reports it.</summary>
    /// <param name="SequenceNumber">The restore point's sequence number.</param>
    /// <param name="RestorePointType">
    ///     The documented restore point type (12 = MODIFY_SETTINGS).
    /// </param>
    /// <param name="Description">The description the creating application supplied.</param>
    /// <param name="CreationTimeUtc">When the point was created, in UTC.</param>
    public sealed record RestorePointInfo(
        uint SequenceNumber,
        uint RestorePointType,
        string Description,
        DateTime CreationTimeUtc
    );

    /// <summary>Outcome of a System Restore operation, with the real native status.</summary>
    /// <param name="Succeeded">Whether the subsystem reported success.</param>
    /// <param name="NativeStatus">The return value Windows reported (0 = success).</param>
    /// <param name="ExceptionText">Throw text when the call itself failed, else null.</param>
    public sealed record SystemRestoreResult(
        bool Succeeded,
        uint NativeStatus,
        string? ExceptionText = null
    );

    /// <summary>
    ///     Lists every restore point Windows reports, newest last. Empty when the list cannot be
    ///     read - enumeration needs elevation, and a machine with protection off has none.
    /// </summary>
    public virtual IReadOnlyList<RestorePointInfo> ListRestorePoints()
    {
        return WmiHelper.Query(
                $"SELECT SequenceNumber, Description, RestorePointType, CreationTime FROM {ClassName}",
                instances =>
                {
                    var points = new List<RestorePointInfo>(instances.Count);
                    foreach (var instance in instances)
                    {
                        if (instance["CreationTime"] is not string raw)
                            continue;

                        try
                        {
                            points.Add(
                                new RestorePointInfo(
                                    instance["SequenceNumber"] is uint sequence ? sequence : 0u,
                                    instance["RestorePointType"] is uint type ? type : 0u,
                                    instance["Description"] as string ?? string.Empty,
                                    ManagementDateTimeConverter.ToDateTime(raw).ToUniversalTime()
                                )
                            );
                        }
                        catch (ArgumentException)
                        {
                            // Skip an unparsable timestamp rather than failing the whole read.
                            _logger.LogDebug(
                                "Skipped a restore point with an unreadable CreationTime: {Raw}",
                                raw
                            );
                        }
                    }

                    return (IReadOnlyList<RestorePointInfo>)points;
                },
                NamespacePath
            ) ?? [];
    }

    /// <summary>
    ///     Newest restore point, or <see langword="null" /> when there is none or the list cannot
    ///     be read.
    /// </summary>
    public virtual DateTime? GetNewestRestorePointUtc()
    {
        DateTime? newest = null;
        foreach (var point in ListRestorePoints())
        {
            if (newest is null || point.CreationTimeUtc > newest.Value)
                newest = point.CreationTimeUtc;
        }

        return newest;
    }

    /// <summary>
    ///     The documented creation frequency in minutes: 0 means Windows never skips, an absent
    ///     value means <see cref="DefaultCreationFrequencyMinutes" />.
    /// </summary>
    public virtual int GetCreationFrequencyMinutes()
    {
        if (
            RegistryService.TryReadValue(
                new RegistryItem(FrequencyKey, FrequencyValueName),
                out var raw,
                _logger
            ) && int.TryParse(raw?.ToString(), out var configured)
        )
            return configured;

        return DefaultCreationFrequencyMinutes;
    }

    /// <summary>
    ///     Whether Windows would refuse a new point because one was created inside the frequency
    ///     window. Unreadable state fails open (false), so a caller attempts the create instead of
    ///     blocking the user on a guess.
    /// </summary>
    public virtual bool IsWithinCreationThrottle(DateTime nowUtc)
    {
        return IsWithinCreationThrottle(
            GetCreationFrequencyMinutes(),
            GetNewestRestorePointUtc(),
            nowUtc
        );
    }

    /// <summary>Creates a restore point. Needs elevation.</summary>
    public virtual SystemRestoreResult CreateRestorePoint(
        string description,
        uint restorePointType = RestorePointTypeModifySettings,
        uint eventType = EventTypeBeginSystemChange
    )
    {
        return Invoke(
            "CreateRestorePoint",
            parameters =>
            {
                parameters["Description"] = description;
                parameters["RestorePointType"] = restorePointType;
                parameters["EventType"] = eventType;
            }
        );
    }

    /// <summary>
    ///     Enables System Protection for one drive, for example <c>C:</c>. Needs elevation.
    /// </summary>
    public virtual SystemRestoreResult EnableProtection(string drive)
    {
        return Invoke("Enable", parameters => parameters["Drive"] = drive);
    }

    /// <summary>Disables System Protection for one drive. Needs elevation.</summary>
    public virtual SystemRestoreResult DisableProtection(string drive)
    {
        return Invoke("Disable", parameters => parameters["Drive"] = drive);
    }

    /// <summary>
    ///     Whether a status means System Protection is not enabled for the drive. Codes only:
    ///     message text is localizable and must never decide this.
    /// </summary>
    public static bool IsProtectionDisabledStatus(uint status) =>
        status is ErrorServiceDisabled or HrServiceDisabled;

    /// <summary>
    ///     The throttle rule on its own: Windows skips a create when the newest point is inside the
    ///     window, so exactly-at-the-boundary is not throttled.
    /// </summary>
    internal static bool IsWithinCreationThrottle(
        int frequencyMinutes,
        DateTime? newestRestorePointUtc,
        DateTime nowUtc
    )
    {
        if (frequencyMinutes <= 0 || newestRestorePointUtc is null)
            return false;

        return (nowUtc - newestRestorePointUtc.Value).TotalMinutes < frequencyMinutes;
    }

    /// <summary>
    ///     Invokes one <c>SystemRestore</c> method and reports its <c>ReturnValue</c>. A throw (no
    ///     elevation, subsystem unavailable) comes back as exception text with
    ///     <see cref="uint.MaxValue" />, a value no documented return code uses.
    /// </summary>
    private SystemRestoreResult Invoke(string method, Action<ManagementBaseObject> fillParameters)
    {
        try
        {
            var scope = new ManagementScope(NamespacePath);
            using var restoreClass = new ManagementClass(
                scope,
                new ManagementPath(ClassName),
                null
            );
            using var inParameters = restoreClass.GetMethodParameters(method);
            fillParameters(inParameters);

            using var outParameters = restoreClass.InvokeMethod(method, inParameters, null);
            var status = outParameters?["ReturnValue"] is uint value ? value : 0u;

            if (status != 0 && !IsProtectionDisabledStatus(status))
                _logger.LogWarning(
                    "[SYSTEMRESTORE][{Method}] failed with native status 0x{Status:X8}",
                    method,
                    status
                );

            return new SystemRestoreResult(status == 0, status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SYSTEMRESTORE][{Method}] threw", method);
            return new SystemRestoreResult(false, uint.MaxValue, ex.Message);
        }
    }
}
