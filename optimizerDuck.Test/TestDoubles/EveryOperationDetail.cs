using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Services;

namespace optimizerDuck.Test.TestDoubles;

/// <summary>
///     One step of every operation, with the facts that operation has. Shared by the tests that
///     have to make a statement about every kind at once, so a new kind is added in one place and
///     every such test fails until it is.
/// </summary>
public static class EveryOperationDetail
{
    public static IReadOnlyList<ChangeDetail> Items { get; } =
    [
        new RegistryValueWriteDetail
        {
            Target = @"HKLM\Test\A",
            ValueName = "V",
            ValueType = "DWord",
            PreviousValue = "1",
            NewValue = "0",
        },
        new RegistryValueRemoveDetail
        {
            Target = @"HKLM\Test\A",
            ValueName = "V",
            ValueType = "DWord",
            PreviousValue = "1",
        },
        new RegistryKeyCreateDetail { Target = @"HKLM\Test\A" },
        new RegistryKeyRemoveDetail { Target = @"HKLM\Test\A" },
        new ServiceStartupDetail
        {
            ServiceName = "DiagTrack",
            PreviousStartupType = ServiceStartupType.Automatic,
            NewStartupType = ServiceStartupType.Disabled,
        },
        new ScheduledTaskEnableDetail
        {
            TaskPath = @"\Microsoft\Test",
            PreviousEnabled = false,
            NewEnabled = true,
        },
        new ScheduledTaskDisableDetail
        {
            TaskPath = @"\Microsoft\Test",
            PreviousEnabled = true,
            NewEnabled = false,
        },
        new PowerPlanActivateDetail
        {
            PlanName = "Duck",
            PreviousPlanName = "Balanced",
            NewPlanName = "Duck",
        },
        new PowerPlanInstallDetail { PlanName = "Duck" },
        new PowerSettingDetail
        {
            SettingId = "1F0B4FA0-8E9E-4D8A-9C4B-1E0A2F5B7C31",
            SettingName = "Processor performance",
            PreviousValue = "AC 100 / DC 50",
            NewValue = "AC 80 / DC 40",
        },
        new HibernationDetail { PreviousPresent = true, NewPresent = false },
        new UsbPowerDetail { DeviceCount = 2 },
    ];

    public static TheoryData<ChangeDetail> All()
    {
        var data = new TheoryData<ChangeDetail>();
        foreach (var detail in Items)
            data.Add(detail);

        return data;
    }
}
