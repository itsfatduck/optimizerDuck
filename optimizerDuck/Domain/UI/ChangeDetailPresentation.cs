using System.Globalization;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Services;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.UI;

/// <summary>
///     One fact of a step, ready to be shown as a chip: a label, an icon and a value. The value is
///     either shown as it is, because a path or a number needs no translation, or it names a
///     resource key of its own, so a word such as a startup type follows the UI language.
/// </summary>
/// <param name="LabelKey">The resource key of the chip's label.</param>
/// <param name="Icon">The chip's icon.</param>
/// <param name="Value">The chip's value, or the key of its text when <paramref name="ValueIsKey" />.</param>
/// <param name="ValueIsKey">Whether the value names a resource key rather than being shown as it is.</param>
public sealed record DetailChip(
    string LabelKey,
    SymbolRegular Icon,
    string Value,
    bool ValueIsKey = false
);

/// <summary>
///     How the record words one step: the action it took, in the language of the run it belongs to,
///     and the facts it acted on.
/// </summary>
public sealed record ChangeDetailRow
{
    /// <summary>The resource key of the action, or null for a step that changed nothing.</summary>
    public string? ActionKey { get; init; }

    /// <summary>The action's argument, when the wording takes one.</summary>
    public string? ActionArg { get; init; }

    /// <summary>The facts of the step, in the order the record shows them.</summary>
    public IReadOnlyList<DetailChip> Chips { get; init; } = [];
}

/// <summary>
///     Maps the facts of a step to the row the record shows, next to
///     <see cref="ChangeKindPresentation" />. It is the only place that knows which fact belongs to
///     which operation and what it is called, so the view never looks an operation up by name.
/// </summary>
public static class ChangeDetailPresentation
{
    /// <summary>
    ///     The row for one step, worded for the run it belongs to. A step that changed something
    ///     states the action and, when it moved between two values, the pair it moved between;
    ///     anything else states no action and shows the state it found instead. An operation this
    ///     build does not know produces an empty row, which leaves the row on the log description.
    /// </summary>
    /// <param name="detail">The facts of the step, or null when it recorded none.</param>
    /// <param name="kind">What the step did, which decides whether it is phrased as an action.</param>
    /// <param name="operation">Which run the row belongs to, an apply or a revert.</param>
    public static ChangeDetailRow For(
        ChangeDetail? detail,
        ChangeKind kind,
        ChangeRecordOperation operation
    )
    {
        if (detail is null)
            return new ChangeDetailRow();

        var shape = ShapeOf(detail);
        var chips = new List<DetailChip>(shape.Chips);

        if (ReasonKey(detail.Reason) is { } reason)
            chips.Add(
                new DetailChip("Optimizer.Details.Field.Reason", SymbolRegular.Info24, reason, true)
            );

        // A step that changed something shows what it moved between; anything else shows the state
        // it found, because there is no after to speak of.
        if (kind == ChangeKind.Change && detail.HasValuePair)
        {
            if (shape.Pair is { } pair)
            {
                chips.Add(
                    new DetailChip(
                        "Optimizer.Details.Field.Previous",
                        SymbolRegular.Clock24,
                        pair.Previous ?? "Optimizer.Details.Field.Previous.None",
                        pair.Previous is null || pair.PreviousIsKey
                    )
                );

                if (pair.New is not null)
                    chips.Add(
                        new DetailChip(
                            "Optimizer.Details.Field.New",
                            SymbolRegular.Edit24,
                            pair.New,
                            pair.NewIsKey
                        )
                    );
            }
        }
        else if (shape.State is { } state)
        {
            chips.Add(state);
        }

        return new ChangeDetailRow
        {
            ActionKey =
                kind == ChangeKind.Change
                    ? operation == ChangeRecordOperation.Revert
                        ? shape.RevertKey
                        : shape.ApplyKey
                    : null,
            ActionArg = shape.ActionArg,
            Chips = chips,
        };
    }

    /// <summary>
    ///     What each kind of operation shows: the wording of its action, the facts it always
    ///     carries, the pair it moves between when it changes something, and the state a step that
    ///     wrote nothing shows instead.
    /// </summary>
    private static DetailShape ShapeOf(ChangeDetail detail)
    {
        return detail switch
        {
            RegistryValueWriteDetail value => new DetailShape
            {
                ApplyKey = "Optimizer.Details.Op.RegistrySet",
                RevertKey = "Optimizer.Details.Op.RegistryRestore",
                ActionArg = value.ValueName,
                Chips = Facts(
                    Chip("Optimizer.Details.Field.Path", SymbolRegular.FolderOpen24, value.Target),
                    Chip(
                        "Optimizer.Details.Field.ValueName",
                        SymbolRegular.ToggleRight24,
                        value.ValueName
                    ),
                    ValueType(value.ValueType)
                ),
                Pair = new DetailPair(value.PreviousValue, false, value.NewValue, false),
                State = Chip("Optimizer.Details.Field.Value", SymbolRegular.Edit24, value.NewValue),
            },
            RegistryValueRemoveDetail value => new DetailShape
            {
                ApplyKey = "Optimizer.Details.Op.RegistryDelete",
                RevertKey = "Optimizer.Details.Op.RegistryRestore",
                ActionArg = value.ValueName,
                Chips = Facts(
                    Chip("Optimizer.Details.Field.Path", SymbolRegular.FolderOpen24, value.Target),
                    Chip(
                        "Optimizer.Details.Field.ValueName",
                        SymbolRegular.ToggleRight24,
                        value.ValueName
                    ),
                    ValueType(value.ValueType)
                ),
                Pair = new DetailPair(value.PreviousValue, false, null, false),
                State = Chip(
                    "Optimizer.Details.Field.Value",
                    SymbolRegular.Edit24,
                    value.PreviousValue
                ),
            },
            RegistryKeyCreateDetail key => new DetailShape
            {
                ApplyKey = "Optimizer.Details.Op.RegistryCreateKey",
                RevertKey = "Optimizer.Details.Op.RegistryDeleteKey",
                Chips = Facts(
                    Chip("Optimizer.Details.Field.Path", SymbolRegular.FolderOpen24, key.Target)
                ),
            },
            RegistryKeyRemoveDetail key => new DetailShape
            {
                ApplyKey = "Optimizer.Details.Op.RegistryDeleteKey",
                RevertKey = "Optimizer.Details.Op.RegistryRestoreKey",
                Chips = Facts(
                    Chip("Optimizer.Details.Field.Path", SymbolRegular.FolderOpen24, key.Target)
                ),
            },
            ServiceStartupDetail service => new DetailShape
            {
                ApplyKey = "Optimizer.Details.Op.ServiceStartup",
                RevertKey = "Optimizer.Details.Op.ServiceRestore",
                ActionArg = service.ServiceName,
                Chips = Facts(
                    Chip(
                        "Optimizer.Details.Field.Service",
                        SymbolRegular.Shield24,
                        service.ServiceName
                    )
                ),
                Pair = new DetailPair(
                    StartupKey(service.PreviousStartupType),
                    true,
                    StartupKey(service.NewStartupType),
                    true
                ),
                State = Chip(
                    "Optimizer.Details.Field.StartupState",
                    SymbolRegular.Shield24,
                    StartupKey(service.NewStartupType ?? service.PreviousStartupType),
                    true
                ),
            },
            ScheduledTaskEnableDetail task => new DetailShape
            {
                ApplyKey = "Optimizer.Details.Op.TaskEnable",
                RevertKey = "Optimizer.Details.Op.TaskDisable",
                ActionArg = task.TaskPath,
                Chips = TaskChips(task.TaskPath),
                Pair = new DetailPair(
                    StateKey(task.PreviousEnabled),
                    true,
                    StateKey(task.NewEnabled),
                    true
                ),
                State = Chip(
                    "Optimizer.Details.Field.TaskState",
                    SymbolRegular.Clock24,
                    StateKey(task.NewEnabled ?? task.PreviousEnabled),
                    true
                ),
            },
            ScheduledTaskDisableDetail task => new DetailShape
            {
                ApplyKey = "Optimizer.Details.Op.TaskDisable",
                RevertKey = "Optimizer.Details.Op.TaskEnable",
                ActionArg = task.TaskPath,
                Chips = TaskChips(task.TaskPath),
                Pair = new DetailPair(
                    StateKey(task.PreviousEnabled),
                    true,
                    StateKey(task.NewEnabled),
                    true
                ),
                State = Chip(
                    "Optimizer.Details.Field.TaskState",
                    SymbolRegular.Clock24,
                    StateKey(task.NewEnabled ?? task.PreviousEnabled),
                    true
                ),
            },
            PowerPlanActivateDetail plan => new DetailShape
            {
                ApplyKey = "Optimizer.Details.Op.PowerPlanActivate",
                RevertKey = "Optimizer.Details.Op.PowerPlanRestore",
                ActionArg = plan.PlanName,
                Chips = PlanChips(plan.PlanName),
                Pair = new DetailPair(plan.PreviousPlanName, false, plan.NewPlanName, false),
            },
            PowerPlanInstallDetail plan => new DetailShape
            {
                ApplyKey = "Optimizer.Details.Op.PowerPlanInstall",
                RevertKey = "Optimizer.Details.Op.PowerPlanUninstall",
                ActionArg = plan.PlanName,
                Chips = PlanChips(plan.PlanName),
            },
            PowerSettingDetail setting => new DetailShape
            {
                ApplyKey = "Optimizer.Details.Op.PowerSetting",
                RevertKey = "Optimizer.Details.Op.PowerSettingRestore",
                ActionArg = setting.SettingName ?? setting.SettingId,
                Chips = Facts(
                    Chip(
                        "Optimizer.Details.Field.Setting",
                        SymbolRegular.Gauge24,
                        setting.SettingName ?? setting.SettingId
                    )
                ),
                Pair = new DetailPair(setting.PreviousValue, false, setting.NewValue, false),
                State = Chip(
                    "Optimizer.Details.Field.Value",
                    SymbolRegular.Edit24,
                    setting.NewValue ?? setting.PreviousValue
                ),
            },
            HibernationDetail hibernation => new DetailShape
            {
                ApplyKey = "Optimizer.Details.Op.Hibernation",
                RevertKey =
                    hibernation.NewPresent == true
                        ? "Optimizer.Details.Op.HibernationEnable"
                        : "Optimizer.Details.Op.HibernationRestore",
                Pair = new DetailPair(
                    StateKey(hibernation.PreviousPresent) ?? "Optimizer.Details.Value.Unknown",
                    true,
                    StateKey(hibernation.NewPresent),
                    true
                ),
                State = Chip(
                    "Optimizer.Details.Field.HibernationState",
                    SymbolRegular.BatteryCharge24,
                    StateKey(hibernation.NewPresent ?? hibernation.PreviousPresent),
                    true
                ),
            },
            UsbPowerDetail usb => new DetailShape
            {
                ApplyKey = "Optimizer.Details.Op.UsbPower",
                RevertKey = "Optimizer.Details.Op.UsbPowerRestore",
                Chips = Facts(
                    new DetailChip(
                        "Optimizer.Details.Field.Devices",
                        SymbolRegular.NetworkAdapter16,
                        usb.DeviceCount.ToString(CultureInfo.InvariantCulture)
                    )
                ),
            },

            // An operation a record file names but this build does not know: its row keeps the
            // description the log wrote.
            _ => new DetailShape(),
        };
    }

    /// <summary>The chips of a row, without the ones that have nothing to show.</summary>
    private static IReadOnlyList<DetailChip> Facts(params DetailChip?[] chips) =>
        [.. chips.Where(chip => chip is not null).Select(chip => chip!)];

    private static IReadOnlyList<DetailChip> TaskChips(string taskPath) =>
        Facts(Chip("Optimizer.Details.Field.Task", SymbolRegular.Clock24, taskPath));

    private static IReadOnlyList<DetailChip> PlanChips(string planName) =>
        Facts(Chip("Optimizer.Details.Field.PowerPlan", SymbolRegular.BatteryCharge24, planName));

    /// <summary>A chip for one fact, or none when the step carries nothing to show in it.</summary>
    private static DetailChip? Chip(
        string labelKey,
        SymbolRegular icon,
        string? value,
        bool valueIsKey = false
    ) => string.IsNullOrEmpty(value) ? null : new DetailChip(labelKey, icon, value, valueIsKey);

    /// <summary>
    ///     A registry value's kind as Windows itself names it, so the chip reads the same in every
    ///     language and matches what a user sees in the registry editor.
    /// </summary>
    private static DetailChip? ValueType(string? valueType) =>
        valueType switch
        {
            null or "" => null,
            "DWord" => new DetailChip(
                "Optimizer.Details.Field.Type",
                SymbolRegular.Code24,
                "REG_DWORD"
            ),
            "QWord" => new DetailChip(
                "Optimizer.Details.Field.Type",
                SymbolRegular.Code24,
                "REG_QWORD"
            ),
            "String" => new DetailChip(
                "Optimizer.Details.Field.Type",
                SymbolRegular.Code24,
                "REG_SZ"
            ),
            "ExpandString" => new DetailChip(
                "Optimizer.Details.Field.Type",
                SymbolRegular.Code24,
                "REG_EXPAND_SZ"
            ),
            "MultiString" => new DetailChip(
                "Optimizer.Details.Field.Type",
                SymbolRegular.Code24,
                "REG_MULTI_SZ"
            ),
            "Binary" => new DetailChip(
                "Optimizer.Details.Field.Type",
                SymbolRegular.Code24,
                "REG_BINARY"
            ),
            _ => new DetailChip("Optimizer.Details.Field.Type", SymbolRegular.Code24, valueType),
        };

    private static string? StartupKey(ServiceStartupType? startupType) =>
        startupType switch
        {
            ServiceStartupType.Automatic => "Optimizer.Details.Value.Startup.Automatic",
            ServiceStartupType.AutomaticDelayedStart =>
                "Optimizer.Details.Value.Startup.AutomaticDelayed",
            ServiceStartupType.Manual => "Optimizer.Details.Value.Startup.Manual",
            ServiceStartupType.Disabled => "Optimizer.Details.Value.Startup.Disabled",
            _ => null,
        };

    /// <summary>
    ///     A state that is on or off as the user reads it, or null when the state is not known: an
    ///     unknown state is its own answer and is never shown as off.
    /// </summary>
    private static string? StateKey(bool? state) =>
        state switch
        {
            true => "Optimizer.Details.Value.Enabled",
            false => "Optimizer.Details.Value.Startup.Disabled",
            _ => null,
        };

    /// <summary>
    ///     The sentence for a reason a provider recorded. A reason the UI does not know yet is left
    ///     out rather than shown as a code.
    /// </summary>
    private static string? ReasonKey(string? reason) =>
        reason switch
        {
            "service.notFound" => "Optimizer.Details.Reason.ServiceNotFound",
            "task.notFound" => "Optimizer.Details.Reason.TaskNotFound",
            "registry.valueAbsent" => "Optimizer.Details.Reason.RegistryValueAbsent",
            "registry.keyExists" => "Optimizer.Details.Reason.RegistryKeyExists",
            "registry.keyAbsent" => "Optimizer.Details.Reason.RegistryKeyAbsent",
            "usb.noDevices" => "Optimizer.Details.Reason.UsbNoDevices",
            _ => null,
        };

    /// <summary>The two values a step moved between, which a revert reads the other way round.</summary>
    private sealed record DetailPair(
        string? Previous,
        bool PreviousIsKey,
        string? New,
        bool NewIsKey
    );

    /// <summary>Everything one kind of operation contributes to its row.</summary>
    private sealed record DetailShape
    {
        public string ApplyKey { get; init; } = string.Empty;

        public string? RevertKey { get; init; }

        public string? ActionArg { get; init; }

        public IReadOnlyList<DetailChip> Chips { get; init; } = [];

        public DetailPair? Pair { get; init; }

        public DetailChip? State { get; init; }
    }
}
