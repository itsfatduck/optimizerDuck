using System.Globalization;
using optimizerDuck.Domain.Optimizations.Models.Services;

namespace optimizerDuck.Domain.Execution;

/// <summary>
///     The one place that maps the typed facts of a step to the flat fields the record file stores,
///     and back. The file keeps its field names and its shape, so a record written by an earlier
///     build still loads; what changes is that nothing else has to know how a detail is laid out.
/// </summary>
public static class ChangeDetailCodec
{
    /// <summary>
    ///     The words the record file uses for a state that is on or off. Old records carry the same
    ///     words, and the states they describe are still on or off, so one representation serves
    ///     both and no record has to be rewritten.
    /// </summary>
    private const string StateEnabled = "Enabled";

    private const string StateDisabled = "Disabled";

    /// <summary>
    ///     The step as the record file stores the facts of a detail, or the step with no facts when
    ///     the provider recorded none. The detail fields are written from the kind the step is,
    ///     so a field that operation has no meaning for stays empty.
    /// </summary>
    public static ChangeRecordStep WithDetail(this ChangeRecordStep step, ChangeDetail? detail)
    {
        if (detail is null)
            return step;

        // What every kind of step stores the same way: what it did, why it wrote nothing, and
        // whether it moved between two values.
        var stored = step with
        {
            Operation = detail.Operation,
            Reason = detail.Reason,
            HasValuePair = detail.HasValuePair,
        };

        return detail switch
        {
            RegistryValueWriteDetail value => stored with
            {
                Target = value.Target,
                ValueName = value.ValueName,
                ValueType = value.ValueType,
                PreviousValue = value.PreviousValue,
                NewValue = value.NewValue,
            },
            RegistryValueRemoveDetail value => stored with
            {
                Target = value.Target,
                ValueName = value.ValueName,
                ValueType = value.ValueType,
                PreviousValue = value.PreviousValue,
            },
            RegistryKeyCreateDetail key => stored with { Target = key.Target },
            RegistryKeyRemoveDetail key => stored with { Target = key.Target },
            ServiceStartupDetail service => stored with
            {
                Target = service.ServiceName,
                PreviousValue = service.PreviousStartupType?.ToString(),
                NewValue = service.NewStartupType?.ToString(),
            },
            ScheduledTaskEnableDetail task => stored with
            {
                Target = task.TaskPath,
                PreviousValue = StateWord(task.PreviousEnabled),
                NewValue = StateWord(task.NewEnabled),
            },
            ScheduledTaskDisableDetail task => stored with
            {
                Target = task.TaskPath,
                PreviousValue = StateWord(task.PreviousEnabled),
                NewValue = StateWord(task.NewEnabled),
            },
            PowerPlanActivateDetail plan => stored with
            {
                Target = plan.PlanName,
                PreviousValue = plan.PreviousPlanName,
                NewValue = plan.NewPlanName,
            },
            PowerPlanInstallDetail plan => stored with { Target = plan.PlanName },
            PowerSettingDetail setting => stored with
            {
                Target = setting.SettingId,
                DisplayName = setting.SettingName,
                PreviousValue = setting.PreviousValue,
                NewValue = setting.NewValue,
            },
            HibernationDetail hibernation => stored with
            {
                PreviousValue = StateWord(hibernation.PreviousPresent),
                NewValue = StateWord(hibernation.NewPresent),
            },
            UsbPowerDetail usb => stored with
            {
                NewValue = usb.DeviceCount.ToString(CultureInfo.InvariantCulture),
            },

            // A detail this build cannot lay out is stored without facts rather than as a guess.
            _ => step,
        };
    }

    /// <summary>
    ///     The facts of a stored step, as the kind of operation it is, or <see langword="null" />
    ///     for an operation this build does not know: that row falls back to the English
    ///     description the log uses, and nothing else in the record changes. A stored value the
    ///     operation has no meaning for is ignored rather than shown.
    /// </summary>
    public static ChangeDetail? Decode(ChangeRecordStep step)
    {
        return step.Operation switch
        {
            "registry.write" => new RegistryValueWriteDetail
            {
                Target = step.Target ?? string.Empty,
                ValueName = step.ValueName ?? string.Empty,
                ValueType = step.ValueType,
                PreviousValue = step.PreviousValue,
                NewValue = step.NewValue,
                Reason = step.Reason,
            },
            "registry.delete" => new RegistryValueRemoveDetail
            {
                Target = step.Target ?? string.Empty,
                ValueName = step.ValueName ?? string.Empty,
                ValueType = step.ValueType,
                PreviousValue = step.PreviousValue,
                Reason = step.Reason,
            },
            "registry.createKey" => new RegistryKeyCreateDetail
            {
                Target = step.Target ?? string.Empty,
                Reason = step.Reason,
            },
            "registry.deleteKey" => new RegistryKeyRemoveDetail
            {
                Target = step.Target ?? string.Empty,
                Reason = step.Reason,
            },
            "service.startup" => new ServiceStartupDetail
            {
                ServiceName = step.Target ?? string.Empty,
                PreviousStartupType = StartupType(step.PreviousValue),
                NewStartupType = StartupType(step.NewValue),
                Reason = step.Reason,
            },
            "task.enable" => new ScheduledTaskEnableDetail
            {
                TaskPath = step.Target ?? string.Empty,
                PreviousEnabled = State(step.PreviousValue) ?? false,
                NewEnabled = State(step.NewValue) ?? true,
                Reason = step.Reason,
            },
            "task.disable" => new ScheduledTaskDisableDetail
            {
                TaskPath = step.Target ?? string.Empty,
                PreviousEnabled = State(step.PreviousValue) ?? true,
                NewEnabled = State(step.NewValue) ?? false,
                Reason = step.Reason,
            },
            "power.plan" => new PowerPlanActivateDetail
            {
                PlanName = step.Target ?? string.Empty,
                PreviousPlanName = step.PreviousValue,
                NewPlanName = step.NewValue,
                Reason = step.Reason,
            },
            "power.planInstall" => new PowerPlanInstallDetail
            {
                PlanName = step.Target ?? string.Empty,
                Reason = step.Reason,
            },
            "power.setting" => new PowerSettingDetail
            {
                SettingId = step.Target ?? string.Empty,
                SettingName = step.DisplayName,
                PreviousValue = step.PreviousValue,
                NewValue = step.NewValue,
                Reason = step.Reason,
            },
            "hibernation" => new HibernationDetail
            {
                PreviousPresent = State(step.PreviousValue),
                NewPresent = State(step.NewValue),
                Reason = step.Reason,
            },
            "usb.power" => new UsbPowerDetail
            {
                DeviceCount = int.TryParse(
                    step.NewValue,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var count
                )
                    ? count
                    : 0,
                Reason = step.Reason,
            },
            _ => null,
        };
    }

    /// <summary>
    ///     The word the record file uses for a state, or null when the state is unknown.
    /// </summary>
    private static string? StateWord(bool? state) =>
        state switch
        {
            true => StateEnabled,
            false => StateDisabled,
            _ => null,
        };

    /// <summary>
    ///     A state read back from the record file. A state the file does not carry, or one that
    ///     says it is unknown, is unknown rather than off: a revert must not guess, and a row must
    ///     not claim the machine was in a state it never recorded.
    /// </summary>
    private static bool? State(string? word) =>
        word switch
        {
            StateEnabled => true,
            StateDisabled => false,
            _ => null,
        };

    private static ServiceStartupType? StartupType(string? word) =>
        Enum.TryParse<ServiceStartupType>(word, out var parsed) ? parsed : null;
}
