using System.Management;
using System.Runtime.Versioning;

namespace optimizerDuck.Services.System.Primitives;

/// <summary>
///     Per-device USB power management over <c>root\wmi</c>'s <c>MSPower_DeviceEnable</c>, read
///     and written through the in-process WMI client.
///     Microsoft does not document this WMI class and documents no API for the per-device "allow
///     the computer to turn off this device" setting. Every operation fails open: no devices, a
///     failed query, or a refused write never throws out of this service.
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

    /// <summary>
    ///     How many devices a write pass actually changed, and which devices refused the write.
    ///     The devices that were changed are still restorable, so a refusal never hides them.
    /// </summary>
    public sealed record UsbPowerWriteResult(int ChangedCount, IReadOnlyList<string> FailedDevices);

    /// <summary>
    ///     Captures the current state of every USB root-hub device, which is what a revert
    ///     restores. Empty when the class is unavailable, so callers treat it as "nothing to do".
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
    ///     The devices a write pass has to touch, given the states Windows reports and the
    ///     requested target. A null target leaves a device alone, and a device already in the
    ///     requested state is not written again. Pure, so the choice is testable without WMI.
    /// </summary>
    internal static IReadOnlyList<UsbPowerState> SelectChanges(
        IReadOnlyList<UsbPowerState> current,
        Func<UsbPowerState, bool?> target
    )
    {
        var selected = new List<UsbPowerState>();
        foreach (var state in current)
        {
            var want = target(state);
            if (want is null || want == state.Enable)
                continue;

            selected.Add(state with { Enable = want.Value });
        }

        return selected;
    }

    private static UsbPowerWriteResult? Write(Func<UsbPowerState, bool?> target)
    {
        return WmiHelper.Query(
            DeviceQuery,
            devices =>
            {
                var byName = new Dictionary<string, ManagementObject>(
                    StringComparer.OrdinalIgnoreCase
                );
                var current = new List<UsbPowerState>(devices.Count);
                foreach (var device in devices)
                {
                    var state = ToState(device);
                    if (state is null)
                        continue;

                    current.Add(state);
                    byName[state.InstanceName] = device;
                }

                var changed = 0;
                var failed = new List<string>();
                foreach (var planned in SelectChanges(current, target))
                {
                    if (!byName.TryGetValue(planned.InstanceName, out var device))
                        continue;

                    try
                    {
                        device["Enable"] = planned.Enable;
                        device.Put();
                        changed++;
                    }
                    catch
                    {
                        // One device refusing must not throw away the devices that were already
                        // changed: the caller still has to record their previous state.
                        failed.Add(planned.InstanceName);
                    }
                }

                return new UsbPowerWriteResult(changed, failed);
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
