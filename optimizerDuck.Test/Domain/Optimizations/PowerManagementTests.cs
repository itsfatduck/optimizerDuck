using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Categories;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Domain.UI;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Test.Domain.Optimizations;

public class PowerManagementTests
{
    [Fact]
    public void RegistryPath_WithHklmPrefix_IsReadable()
    {
        var call = new OpCall { Logger = NullLogger.Instance };
        var key = $@"HKCU\SOFTWARE\OptimizerDuckTest\{Guid.NewGuid():N}";

        try
        {
            Assert.True(RegistryService.Write(call, new RegistryItem(key, "Probe", 1)).Ok);
            Assert.Equal(1, RegistryService.Read<int>(new RegistryItem(key, "Probe")));
        }
        finally
        {
            RegistryService.DeleteSubKeyTree(call, new RegistryItem(key));
        }
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(null, true)]
    public void NeedsHibernationChange_OnlySkipsAFileThatIsKnownAbsent(
        bool? wasPresent,
        bool expected
    )
    {
        // An unreadable state is attempted: a failed read is not "already disabled".
        Assert.Equal(
            expected,
            PowerManagement.DisableHibernateAndFastStartup.NeedsHibernationChange(wasPresent)
        );
    }

    [Fact]
    public void Apply_NoUsbDeviceChanged_RecordsSkipWithoutCompensation()
    {
        var call = new OpCall { Logger = NullLogger.Instance };
        var revertStep = new UsbPowerRevertStep
        {
            States =
            [
                new UsbPowerRevertStep.DeviceState
                {
                    InstanceName = @"USB\ROOT_HUB30\1",
                    Enable = false,
                },
            ],
        };

        var result = PowerManagement.DisableUSBPowerSaving.Apply(
            call,
            revertStep,
            new UsbPowerService.UsbPowerWriteResult(0, [])
        );

        Assert.True(result.Ok);
        var step = Assert.Single(call.Changes.Changes);
        Assert.Equal(ChangeKind.Skip, step.Kind);
        Assert.Null(step.Revert);
        Assert.Null(result.Revert);
    }
}
