using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Categories;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Test.Services.OptimizationServices;

/// <summary>
///     Live <c>root\wmi</c> tests. Reads only: no test here flips a real device's power setting,
///     because that is a system change (task 6.4 compares against the PowerShell oracle instead).
/// </summary>
public class UsbPowerServiceTests
{
    [Fact]
    public void Capture_ReturnsOnlyRootHubDevices()
    {
        var captured = UsbPowerService.Capture();

        Assert.All(
            captured,
            state =>
                Assert.Contains(@"USB\ROOT", state.InstanceName, StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Capture_IsStableAcrossCalls()
    {
        var first = UsbPowerService.Capture();
        var second = UsbPowerService.Capture();

        Assert.Equal(
            first.Select(s => s.InstanceName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase),
            second.Select(s => s.InstanceName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Restore_WithNothingCaptured_ChangesNothing()
    {
        // The write loop skips every device when the captured set is empty, so this exercises the
        // "only flip devices that differ" rule without touching a single device.
        var result = UsbPowerService.Restore([]);

        Assert.NotNull(result);
        Assert.Equal(0, result.ChangedCount);
    }

    [Fact]
    public void Capture_ThenRestore_OfCapturedState_WouldBeANoOp()
    {
        // Feeding back exactly what Windows reports must ask for no change at all.
        var captured = UsbPowerService.Capture();

        var result = UsbPowerService.Restore(captured);

        Assert.NotNull(result);
        Assert.Equal(0, result.ChangedCount);
    }

    [Fact]
    public void WriteQuery_ReturnsLocatableInstances()
    {
        // ManagementObject.Put() needs a complete object path. A partial property select leaves
        // __PATH empty, which the provider refuses as "Invalid object" (verified on 2026-09-14),
        // so the write query has to request whole instances. Read only: no device state changes.
        var locatable = new List<bool>();

        var rows = WmiHelper.Query(
            UsbPowerService.DeviceQuery,
            items =>
            {
                foreach (var device in items)
                    locatable.Add(!string.IsNullOrEmpty(device["__PATH"] as string));
                return items.Count;
            },
            @"root\wmi"
        );

        if (rows is not > 0)
            Assert.Skip("This host exposes no root\\wmi power rows, so there is nothing to pin.");

        Assert.All(
            locatable,
            hasPath =>
                Assert.True(hasPath, "the USB write query returned an instance without __PATH")
        );
    }

    [Fact]
    public void SelectChanges_DisableTargetsOnlyDevicesThatAreEnabled()
    {
        var current = new List<UsbPowerService.UsbPowerState>
        {
            new(@"USB\ROOT_HUB30\1", true),
            new(@"USB\ROOT_HUB30\2", false),
        };

        var planned = UsbPowerService.SelectChanges(current, _ => false);

        var only = Assert.Single(planned);
        Assert.Equal(@"USB\ROOT_HUB30\1", only.InstanceName);
        Assert.False(only.Enable);
    }

    [Fact]
    public void SelectChanges_RestoreTargetsOnlyDevicesThatDiffer()
    {
        var current = new List<UsbPowerService.UsbPowerState>
        {
            new(@"USB\ROOT_HUB30\1", false),
            new(@"USB\ROOT_HUB30\2", true),
        };
        var captured = new List<UsbPowerService.UsbPowerState>
        {
            new(@"USB\ROOT_HUB30\1", true),
            new(@"USB\ROOT_HUB30\2", true),
            new(@"USB\ROOT_HUB30\3", false),
        };

        var planned = UsbPowerService.SelectChanges(
            current,
            state => captured.FirstOrDefault(c => c.InstanceName == state.InstanceName)?.Enable
        );

        var only = Assert.Single(planned);
        Assert.Equal(@"USB\ROOT_HUB30\1", only.InstanceName);
        Assert.True(only.Enable);
    }

    [Fact]
    public void SelectChanges_DeviceAlreadyInTheRequestedState_IsLeftAlone()
    {
        var current = new List<UsbPowerService.UsbPowerState> { new(@"USB\ROOT_HUB30\1", true) };

        Assert.Empty(UsbPowerService.SelectChanges(current, _ => true));
    }

    [Fact]
    public void CategoryPartialWrite_RecordsTheRevertStepAndReportsTheFailure()
    {
        // The devices that were changed keep their recorded state, even though the outcome for
        // the write is a failure because another device refused.
        var call = new OpCall { Changes = new ChangeSet(), Logger = NullLogger.Instance };
        var revertStep = new UsbPowerRevertStep
        {
            States =
            [
                new UsbPowerRevertStep.DeviceState
                {
                    InstanceName = @"USB\ROOT_HUB30\1",
                    Enable = true,
                },
            ],
        };
        var write = new UsbPowerService.UsbPowerWriteResult(1, [@"USB\ROOT_HUB30\2"]);

        var result = PowerManagement.DisableUSBPowerSaving.Apply(call, revertStep, write);

        Assert.False(result.Ok);
        var change = Assert.Single(call.Changes.Changes);
        Assert.False(change.Ok);
        Assert.Same(revertStep, change.Revert);
    }
}
