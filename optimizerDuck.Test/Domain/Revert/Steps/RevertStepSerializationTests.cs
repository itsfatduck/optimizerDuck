using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.Revert.Steps;

namespace optimizerDuck.Test.Domain.Revert.Steps;

public class RevertStepSerializationTests
{
    [Fact]
    public void ShellRevertStep_RoundTrip_PreservesAllProperties()
    {
        var original = new ShellRevertStep
        {
            ShellType = ShellType.CMD,
            Command = "sc config TestService start= auto",
        };

        var json = original.ToData();
        var restored = ShellRevertStep.FromData(json);

        Assert.Equal(original.ShellType, restored.ShellType);
        Assert.Equal(original.Command, restored.Command);
        Assert.Equal(original.Type, restored.Type);
    }

    [Fact]
    public void ShellRevertStep_PowerShell_RoundTrip()
    {
        var original = new ShellRevertStep
        {
            ShellType = ShellType.PowerShell,
            Command = "Set-ItemProperty -Path 'HKLM:\\Test' -Name 'Value' -Value 0",
        };

        var json = original.ToData();
        var restored = ShellRevertStep.FromData(json);

        Assert.Equal(ShellType.PowerShell, restored.ShellType);
        Assert.Equal(original.Command, restored.Command);
    }

    [Fact]
    public void ShellRevertStep_WithEmptyCommand_RoundTrip()
    {
        var original = new ShellRevertStep
        {
            ShellType = ShellType.CMD,
            Command = "",
        };

        var json = original.ToData();
        var restored = ShellRevertStep.FromData(json);

        Assert.Equal(string.Empty, restored.Command);
    }

    [Fact]
    public void ServiceRevertStep_RoundTrip_PreservesAllProperties()
    {
        var original = new ServiceRevertStep
        {
            ServiceName = "wuauserv",
            OriginalStartupType = ServiceStartupType.Automatic,
        };

        var json = original.ToData();
        var restored = ServiceRevertStep.FromData(json);

        Assert.Equal(original.ServiceName, restored.ServiceName);
        Assert.Equal(original.OriginalStartupType, restored.OriginalStartupType);
        Assert.Equal(original.Type, restored.Type);
    }

    [Fact]
    public void ServiceRevertStep_Disabled_RoundTrip()
    {
        var original = new ServiceRevertStep
        {
            ServiceName = "Spooler",
            OriginalStartupType = ServiceStartupType.Disabled,
        };

        var json = original.ToData();
        var restored = ServiceRevertStep.FromData(json);

        Assert.Equal("Spooler", restored.ServiceName);
        Assert.Equal(ServiceStartupType.Disabled, restored.OriginalStartupType);
    }

    [Fact]
    public void ServiceRevertStep_Manual_RoundTrip()
    {
        var original = new ServiceRevertStep
        {
            ServiceName = "MpsSvc",
            OriginalStartupType = ServiceStartupType.Manual,
        };

        var json = original.ToData();
        var restored = ServiceRevertStep.FromData(json);

        Assert.Equal(ServiceStartupType.Manual, restored.OriginalStartupType);
    }

    [Fact]
    public void ServiceRevertStep_DelayedStart_RoundTrip()
    {
        var original = new ServiceRevertStep
        {
            ServiceName = "WpnService",
            OriginalStartupType = ServiceStartupType.AutomaticDelayedStart,
        };

        var json = original.ToData();
        var restored = ServiceRevertStep.FromData(json);

        Assert.Equal(ServiceStartupType.AutomaticDelayedStart, restored.OriginalStartupType);
    }

    [Fact]
    public void ServiceRevertStep_EmptyServiceName_RoundTrip()
    {
        var original = new ServiceRevertStep
        {
            ServiceName = "",
            OriginalStartupType = ServiceStartupType.Manual,
        };

        var json = original.ToData();
        var restored = ServiceRevertStep.FromData(json);

        Assert.Equal(string.Empty, restored.ServiceName);
    }

    [Fact]
    public void ScheduledTaskRevertStep_RoundTrip_PreservesAllProperties()
    {
        var original = new ScheduledTaskRevertStep
        {
            FullPath = @"\Microsoft\Windows\TestTask",
            OriginalEnabled = true,
        };

        var json = original.ToData();
        var restored = ScheduledTaskRevertStep.FromData(json);

        Assert.Equal(original.FullPath, restored.FullPath);
        Assert.Equal(original.OriginalEnabled, restored.OriginalEnabled);
        Assert.Equal(original.Type, restored.Type);
    }

    [Fact]
    public void ScheduledTaskRevertStep_DisabledTask_RoundTrip()
    {
        var original = new ScheduledTaskRevertStep
        {
            FullPath = @"\MyApp\Updater",
            OriginalEnabled = false,
        };

        var json = original.ToData();
        var restored = ScheduledTaskRevertStep.FromData(json);

        Assert.False(restored.OriginalEnabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ScheduledTaskRevertStep_WithBothEnabledStates_RoundTrip(bool originalState)
    {
        var original = new ScheduledTaskRevertStep
        {
            FullPath = @"\Test\Task",
            OriginalEnabled = originalState,
        };

        var json = original.ToData();
        var restored = ScheduledTaskRevertStep.FromData(json);

        Assert.Equal(originalState, restored.OriginalEnabled);
    }

    [Fact]
    public void UsbPowerRevertStep_RoundTrip_PreservesAllProperties()
    {
        var original = new UsbPowerRevertStep
        {
            States =
            [
                new UsbPowerRevertStep.DeviceState
                {
                    InstanceName = @"USB\VID_1234\ABC123",
                    Enable = true,
                },
                new UsbPowerRevertStep.DeviceState
                {
                    InstanceName = @"USB\VID_5678\DEF456",
                    Enable = false,
                },
            ],
        };

        var json = original.ToData();
        var restored = UsbPowerRevertStep.FromData(json);

        Assert.Equal(original.Type, restored.Type);
        Assert.Equal(original.States.Count, restored.States.Count);
        Assert.Equal(original.States[0].InstanceName, restored.States[0].InstanceName);
        Assert.Equal(original.States[0].Enable, restored.States[0].Enable);
        Assert.Equal(original.States[1].InstanceName, restored.States[1].InstanceName);
        Assert.Equal(original.States[1].Enable, restored.States[1].Enable);
    }

    [Fact]
    public void UsbPowerRevertStep_EmptyStates_RoundTrip()
    {
        var original = new UsbPowerRevertStep();

        var json = original.ToData();
        var restored = UsbPowerRevertStep.FromData(json);

        Assert.Empty(restored.States);
    }

    [Fact]
    public void RegistryRevertStep_RestorePreviousDWord_RoundTrip()
    {
        var original = new RegistryRevertStep
        {
            Action = RevertAction.RestorePrevious,
            Path = @"HKLM\SOFTWARE\Test",
            Name = "DWordValue",
            Value = 42,
            Kind = RegistryValueKind.DWord,
        };

        var json = original.ToData();
        var restored = RegistryRevertStep.FromData(json);

        Assert.Equal(original.Action, restored.Action);
        Assert.Equal(original.Path, restored.Path);
        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Kind, restored.Kind);
        Assert.Equal(42, restored.Value);
    }

    [Fact]
    public void RegistryRevertStep_RestorePreviousQWord_RoundTrip()
    {
        var original = new RegistryRevertStep
        {
            Action = RevertAction.RestorePrevious,
            Path = @"HKLM\SOFTWARE\Test",
            Name = "QWordValue",
            Value = 9999999999L,
            Kind = RegistryValueKind.QWord,
        };

        var json = original.ToData();
        var restored = RegistryRevertStep.FromData(json);

        Assert.Equal(RegistryValueKind.QWord, restored.Kind);
        Assert.Equal(9999999999L, restored.Value);
    }

    [Fact]
    public void RegistryRevertStep_RestorePreviousString_RoundTrip()
    {
        var original = new RegistryRevertStep
        {
            Action = RevertAction.RestorePrevious,
            Path = @"HKCU\Software\Test",
            Name = "StringValue",
            Value = "Hello World",
            Kind = RegistryValueKind.String,
        };

        var json = original.ToData();
        var restored = RegistryRevertStep.FromData(json);

        Assert.Equal("Hello World", restored.Value);
    }

    [Fact]
    public void RegistryRevertStep_RestorePreviousExpandString_RoundTrip()
    {
        var original = new RegistryRevertStep
        {
            Action = RevertAction.RestorePrevious,
            Path = @"HKLM\SOFTWARE\Test",
            Name = "ExpandValue",
            Value = "%PATH%",
            Kind = RegistryValueKind.ExpandString,
        };

        var json = original.ToData();
        var restored = RegistryRevertStep.FromData(json);

        Assert.Equal(RegistryValueKind.ExpandString, restored.Kind);
        Assert.Equal("%PATH%", restored.Value);
    }

    [Fact]
    public void RegistryRevertStep_RestorePreviousMultiString_RoundTrip()
    {
        var original = new RegistryRevertStep
        {
            Action = RevertAction.RestorePrevious,
            Path = @"HKLM\SOFTWARE\Test",
            Name = "MultiValue",
            Value = new[] { "Line1", "Line2", "Line3" },
            Kind = RegistryValueKind.MultiString,
        };

        var json = original.ToData();
        var restored = RegistryRevertStep.FromData(json);

        Assert.Equal(RegistryValueKind.MultiString, restored.Kind);
        var restoredArray = Assert.IsType<string[]>(restored.Value);
        Assert.Equal(["Line1", "Line2", "Line3"], restoredArray);
    }

    [Fact]
    public void RegistryRevertStep_RestorePreviousBinary_RoundTrip()
    {
        var original = new RegistryRevertStep
        {
            Action = RevertAction.RestorePrevious,
            Path = @"HKLM\SOFTWARE\Test",
            Name = "BinaryValue",
            Value = new byte[] { 0x01, 0x02, 0xFF, 0x00 },
            Kind = RegistryValueKind.Binary,
        };

        var json = original.ToData();
        var restored = RegistryRevertStep.FromData(json);

        Assert.Equal(RegistryValueKind.Binary, restored.Kind);
        var restoredBytes = Assert.IsType<byte[]>(restored.Value);
        Assert.Equal([0x01, 0x02, 0xFF, 0x00], restoredBytes);
    }

    [Fact]
    public void RegistryRevertStep_RestorePreviousNullValue_RoundTrip()
    {
        var original = new RegistryRevertStep
        {
            Action = RevertAction.RestorePrevious,
            Path = @"HKLM\SOFTWARE\Test",
            Name = "NullValue",
            Value = null,
            Kind = RegistryValueKind.String,
        };

        var json = original.ToData();
        var restored = RegistryRevertStep.FromData(json);

        Assert.Null(restored.Value);
    }

    [Fact]
    public void RegistryRevertStep_NoPreviousValue_RoundTrip()
    {
        var original = new RegistryRevertStep
        {
            Action = RevertAction.NoPreviousValue,
            Path = @"HKLM\SOFTWARE\Test",
            Name = "OldValue",
        };

        var json = original.ToData();
        var restored = RegistryRevertStep.FromData(json);

        Assert.Equal(RevertAction.NoPreviousValue, restored.Action);
        Assert.Equal("OldValue", restored.Name);
        Assert.Null(restored.Value);
    }

    [Fact]
    public void RegistryRevertStep_DefaultValueName_RoundTrip()
    {
        var original = new RegistryRevertStep
        {
            Action = RevertAction.RestorePrevious,
            Path = @"HKLM\SOFTWARE\Test",
            Name = null,
            Value = "DefaultData",
            Kind = RegistryValueKind.String,
        };

        var json = original.ToData();
        var restored = RegistryRevertStep.FromData(json);

        Assert.Null(restored.Name);
        Assert.Equal("DefaultData", restored.Value);
    }

    [Fact]
    public void RegistryRevertStep_RestoreKeyAction_RoundTrip()
    {
        var original = new RegistryRevertStep
        {
            Action = RevertAction.RestoreKey,
            Path = @"HKLM\SOFTWARE\Test",
        };

        var json = original.ToData();
        var restored = RegistryRevertStep.FromData(json);

        Assert.Equal(RevertAction.RestoreKey, restored.Action);
        Assert.Equal(@"HKLM\SOFTWARE\Test", restored.Path);
    }

    [Fact]
    public void RegistryRevertStep_DeleteKeyAction_RoundTrip()
    {
        var original = new RegistryRevertStep
        {
            Action = RevertAction.DeleteKey,
            Path = @"HKLM\SOFTWARE\Test\SubKey",
        };

        var json = original.ToData();
        var restored = RegistryRevertStep.FromData(json);

        Assert.Equal(RevertAction.DeleteKey, restored.Action);
        Assert.Null(restored.Name);
    }

    [Fact]
    public void RegistryRevertStep_WithCreatedSubKeys_RoundTrip()
    {
        var original = new RegistryRevertStep
        {
            Action = RevertAction.RestorePrevious,
            Path = @"HKLM\SOFTWARE\Test",
            Name = "Value",
            Value = 1,
            Kind = RegistryValueKind.DWord,
            CreatedSubKeys = new List<string>
            {
                @"HKLM\SOFTWARE\Test\NewKey\SubKey",
                @"HKLM\SOFTWARE\Test\NewKey",
            },
        };

        var json = original.ToData();
        var restored = RegistryRevertStep.FromData(json);

        Assert.NotNull(restored.CreatedSubKeys);
        Assert.Equal(2, restored.CreatedSubKeys.Count);
        Assert.Contains(restored.CreatedSubKeys, k => k.Contains("NewKey"));
    }

    [Fact]
    public void RegistryRevertStep_WithSubSteps_RoundTrip()
    {
        var original = new RegistryRevertStep
        {
            Action = RevertAction.RestoreKeyTree,
            Path = @"HKLM\SOFTWARE\TestTree",
            SubSteps =
            [
                new RegistryRevertStep
                {
                    Action = RevertAction.RestoreKey,
                    Path = @"HKLM\SOFTWARE\TestTree\SubKeyA",
                },
                new RegistryRevertStep
                {
                    Action = RevertAction.RestorePrevious,
                    Path = @"HKLM\SOFTWARE\TestTree\SubKeyA",
                    Name = "Value1",
                    Value = "Data1",
                    Kind = RegistryValueKind.String,
                },
            ],
        };

        var json = original.ToData();
        var restored = RegistryRevertStep.FromData(json);

        Assert.Equal(RevertAction.RestoreKeyTree, restored.Action);
        Assert.NotNull(restored.SubSteps);
        Assert.Equal(2, restored.SubSteps.Count);
        Assert.Equal(RevertAction.RestoreKey, restored.SubSteps[0].Action);
        Assert.Equal(RevertAction.RestorePrevious, restored.SubSteps[1].Action);
        Assert.Equal("Value1", restored.SubSteps[1].Name);
    }
}
