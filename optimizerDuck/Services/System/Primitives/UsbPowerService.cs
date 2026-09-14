using System.Management;
using System.Runtime.Versioning;

namespace optimizerDuck.Services.System.Primitives;

/// <summary>
///     Per-device USB power management over <c>root\wmi</c>'s <c>MSPower_DeviceEnable</c>, using the
///     in-process WMI client (via <c>WmiHelper</c>) instead of a PowerShell script.
///     Microsoft does not document this WMI class and documents no API for the per-device "allow the
///     computer to turn off this device" setting, so the surface underneath is unchanged - only the
///     client is. Every operation fails open: no devices, a failed query, or a refused write never
///     throws out of this service.
/// </summary>
[SupportedOSPlatform("windows")]
public static class UsbPowerService
{
    private const string NamespacePath = @"root\wmi";
    private const string RootHubMarker = @"USB\ROOT";

    // SELECT * on purpose: with a partial property select, System.Management returns objects
    // whose __PATH is empty (EnumerationOptions.EnsureLocatable defaults to false), and
    // ManagementObject.Put() then fails with "Invalid object". Verified against live root\wmi on
    // 2026-09-14: the partial select failed the write, the full select succeeded.
    internal const string DeviceQuery = "SELECT * FROM MSPower_DeviceEnable";

    /// <summary>One device and the USB power-management state Windows reports for it.</summary>
    /// <param name="InstanceName">The WMI device instance name (used to match on revert).</param>
    /// <param name="Enable">Whether Windows may power the device down to save energy.</param>
    public sealed record UsbPowerState(string InstanceName, bool Enable);

    /// <summary>How many devices a write pass actually changed (already-correct devices are left alone).</summary>
    public sealed record UsbPowerWriteResult(int ChangedCount);

    /// <summary>
    ///     Captures the current state of every USB root-hub device, which is what a revert restores.
    ///     Empty when the class is unavailable, so callers treat it as "nothing to do".
    /// </summary>
    public static IReadOnlyList<UsbPowerState> Capture()
    {
        return WmiHelper.Query(
                DeviceQuery,
                static devices =>
                    (IReadOnlyList<UsbPowerState>)
                        devices
                            .Select(ToState)
                            .Where(static state => state is not null)
                            .Select(static state => state!)
                            .ToList(),
                NamespacePath
            ) ?? [];
    }

    /// <summary>
    ///     Turns power saving off for every root-hub device that currently has it on. Null when the
    ///     query failed, so a caller can record a failed change instead of an empty success.
    /// </summary>
    public static UsbPowerWriteResult? Disable()
    {
        return Write(static _ => false);
    }

    /// <summary>
    ///     Restores captured states, writing only the devices whose state differs from the captured
    ///     one. Null when the query failed.
    /// </summary>
    public static UsbPowerWriteResult? Restore(IEnumerable<UsbPowerState> captured)
    {
        var wanted = captured.ToDictionary(
            static state => state.InstanceName,
            static state => state.Enable,
            StringComparer.OrdinalIgnoreCase
        );

        return Write(state =>
            wanted.TryGetValue(state.InstanceName, out var enable) ? enable : null
        );
    }

    /// <summary>
    ///     Applies <paramref name="target" /> to every matching device; a null target means "leave
    ///     this device alone". Devices already in the requested state are not written.
    /// </summary>
    private static UsbPowerWriteResult? Write(Func<UsbPowerState, bool?> target)
    {
        return WmiHelper.Query(
            DeviceQuery,
            devices =>
            {
                var changed = 0;
                foreach (var device in devices)
                {
                    var state = ToState(device);
                    if (state is null)
                        continue;

                    var want = target(state);
                    if (want is null || want == state.Enable)
                        continue;

                    device["Enable"] = want.Value;
                    device.Put();
                    changed++;
                }

                return new UsbPowerWriteResult(changed);
            },
            NamespacePath
        );
    }

    private static UsbPowerState? ToState(ManagementObject device)
    {
        if (device["InstanceName"] is not string name || string.IsNullOrWhiteSpace(name))
            return null;

        if (!name.Contains(RootHubMarker, StringComparison.OrdinalIgnoreCase))
            return null;

        return new UsbPowerState(name, device["Enable"] is true);
    }
}
