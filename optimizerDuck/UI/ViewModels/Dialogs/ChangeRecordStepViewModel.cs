using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Configuration;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Dialogs;

/// <summary>One fact about a step, shown as a chip with its own icon and label.</summary>
public sealed record RecordFieldViewModel(SymbolRegular Icon, string Label, string Value);

/// <summary>
///     One recorded step, as the record shows it. The step keeps its English description and its
///     values for the log; here the row is rebuilt from the structured facts so it says what the
///     run actually did, in the UI language, and never claims more than that.
/// </summary>
public sealed class ChangeRecordStepViewModel
{
    public ChangeRecordStepViewModel(
        ChangeRecordStep step,
        ChangeRecordOperation operation = ChangeRecordOperation.Apply
    )
    {
        Operation = operation;
        Kind = step.Kind;
        Ok = step.Ok;

        var display = ChangeKindPresentation.ForStep(step);
        Icon = display.Icon;
        KindLabel = Loc.Instance[display.LabelKey];

        Name = ProviderName(step.Name);
        Description = step.Description;
        Detail = step.Error;

        // Only a step that changed something is phrased as an action. A step that wrote nothing
        // states what it found instead, so the row can never read as if it had written.
        OperationLabel = step.Kind == ChangeKind.Change ? ResolveActionLabel(step, operation) : null;
        Fields = BuildFields(step);
    }

    /// <summary>Builds a row straight from a step that is still in memory, for the failure list.</summary>
    /// <param name="change">The recorded step.</param>
    public ChangeRecordStepViewModel(
        Change change,
        ChangeRecordOperation operation = ChangeRecordOperation.Apply
    )
        : this(
            new ChangeRecordStep
            {
                Name = change.Name,
                Description = change.Description,
                Kind = change.Kind,
                Ok = change.Ok,
                Error = change.Error,
                Operation = change.Detail?.Operation,
                Target = change.Detail?.Target,
                ValueName = change.Detail?.ValueName,
                ValueType = change.Detail?.ValueType,
                PreviousValue = change.Detail?.PreviousValue,
                NewValue = change.Detail?.NewValue,
                HasValuePair = change.Detail?.HasValuePair ?? false,
                Reason = change.Detail?.Reason,
            },
            operation
        )
    {
        Index = change.Index;
        ErrorDetail = change.ErrorDetail;
    }

    /// <summary>The step's position in the run, for the failure list.</summary>
    public int Index { get; }

    /// <summary>The raw failure text, kept for a support report.</summary>
    public string? ErrorDetail { get; }

    /// <summary>Whether the raw failure text is worth showing.</summary>
    public bool HasErrorDetail => !string.IsNullOrWhiteSpace(ErrorDetail);

    /// <summary>Which run this row belongs to, which decides the wording of the step.</summary>
    public ChangeRecordOperation Operation { get; }

    /// <summary>The kind of step, which decides the accent colour of the row.</summary>
    public ChangeKind Kind { get; }

    /// <summary>Whether the step succeeded, which decides whether it is shown as a failure.</summary>
    public bool Ok { get; }

    public SymbolRegular Icon { get; }

    public string KindLabel { get; }

    public string Name { get; }

    /// <summary>The English description written for the log, kept as the last resort.</summary>
    public string Description { get; }

    /// <summary>
    ///     Whether the row has nothing but the log text to show. A row that states an action or
    ///     carries facts says everything it can, and the English line the log uses would only
    ///     repeat it, or worse, describe work the step never did.
    /// </summary>
    public bool ShowDescription => !HasOperation && !HasFields;

    /// <summary>What the step did, in the UI language, when the step changed something.</summary>
    public string? OperationLabel { get; }

    public bool HasOperation => !string.IsNullOrEmpty(OperationLabel);

    /// <summary>The facts of the step: what it touched, and the values around it.</summary>
    public IReadOnlyList<RecordFieldViewModel> Fields { get; }

    public bool HasFields => Fields.Count > 0;

    /// <summary>The failure message, when the step failed.</summary>
    public string? Detail { get; }

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    /// <summary>What the step did, worded for the run it belongs to.</summary>
    private static string? ResolveActionLabel(ChangeRecordStep step, ChangeRecordOperation operation)
    {
        var target = step.Target ?? string.Empty;

        return operation == ChangeRecordOperation.Revert
            ? RevertLabel(step, target)
            : ApplyLabel(step, target);
    }

    private static string? ApplyLabel(ChangeRecordStep step, string target)
    {
        return step.Operation switch
        {
            "registry.write" => Loc.Instance[
                "Optimizer.Details.Op.RegistrySet",
                step.ValueName ?? string.Empty
            ],
            "registry.delete" => Loc.Instance[
                "Optimizer.Details.Op.RegistryDelete",
                step.ValueName ?? string.Empty
            ],
            "registry.createKey" => Loc.Instance["Optimizer.Details.Op.RegistryCreateKey"],
            "registry.deleteKey" => Loc.Instance["Optimizer.Details.Op.RegistryDeleteKey"],
            "service.startup" => Loc.Instance["Optimizer.Details.Op.ServiceStartup", target],
            "task.enable" => Loc.Instance["Optimizer.Details.Op.TaskEnable", target],
            "task.disable" => Loc.Instance["Optimizer.Details.Op.TaskDisable", target],
            "power.plan" => Loc.Instance["Optimizer.Details.Op.PowerPlanActivate", target],
            "power.planInstall" => Loc.Instance["Optimizer.Details.Op.PowerPlanInstall", target],
            "power.setting" => Loc.Instance["Optimizer.Details.Op.PowerSetting", target],
            "hibernation" => Loc.Instance["Optimizer.Details.Op.Hibernation"],
            "usb.power" => Loc.Instance["Optimizer.Details.Op.UsbPower"],
            _ => null,
        };
    }

    /// <summary>
    ///     A revert puts things back: a value is restored, a key created by the apply is deleted, a
    ///     task is toggled the other way, and hibernation is turned back on when it was on before.
    /// </summary>
    private static string? RevertLabel(ChangeRecordStep step, string target)
    {
        return step.Operation switch
        {
            "registry.write" or "registry.delete" => Loc.Instance[
                "Optimizer.Details.Op.RegistryRestore",
                step.ValueName ?? string.Empty
            ],
            "registry.createKey" => Loc.Instance["Optimizer.Details.Op.RegistryDeleteKey"],
            "registry.deleteKey" => Loc.Instance["Optimizer.Details.Op.RegistryRestoreKey"],
            "service.startup" => Loc.Instance["Optimizer.Details.Op.ServiceRestore", target],
            "task.enable" => Loc.Instance["Optimizer.Details.Op.TaskDisable", target],
            "task.disable" => Loc.Instance["Optimizer.Details.Op.TaskEnable", target],
            "power.plan" => Loc.Instance["Optimizer.Details.Op.PowerPlanRestore", target],
            "power.planInstall" => Loc.Instance["Optimizer.Details.Op.PowerPlanUninstall", target],
            "power.setting" => Loc.Instance["Optimizer.Details.Op.PowerSettingRestore", target],
            "hibernation" => step.NewValue == "Enabled"
                ? Loc.Instance["Optimizer.Details.Op.HibernationEnable"]
                : Loc.Instance["Optimizer.Details.Op.HibernationRestore"],
            "usb.power" => Loc.Instance["Optimizer.Details.Op.UsbPowerRestore"],
            _ => null,
        };
    }

    private static List<RecordFieldViewModel> BuildFields(ChangeRecordStep step)
    {
        var fields = new List<RecordFieldViewModel>();
        var target = step.Target ?? string.Empty;

        // The USB pass touches every root hub at once, so what it has to show is how many.
        if (step.Operation == "usb.power")
        {
            fields.Add(
                new RecordFieldViewModel(
                    SymbolRegular.NetworkAdapter16,
                    Loc.Instance["Optimizer.Details.Field.Devices"],
                    step.NewValue ?? "0"
                )
            );
            return fields;
        }

        var subject = SubjectOf(step.Operation);

        if (!string.IsNullOrEmpty(target))
            fields.Add(new RecordFieldViewModel(subject.Icon, Loc.Instance[subject.LabelKey], target));

        if (!string.IsNullOrEmpty(step.ValueName))
            fields.Add(
                new RecordFieldViewModel(
                    SymbolRegular.ToggleRight24,
                    Loc.Instance["Optimizer.Details.Field.ValueName"],
                    step.ValueName
                )
            );

        if (!string.IsNullOrEmpty(step.ValueType))
            fields.Add(
                new RecordFieldViewModel(
                    SymbolRegular.Code24,
                    Loc.Instance["Optimizer.Details.Field.Type"],
                    DescribeValueType(step.ValueType)
                )
            );

        // Why nothing was written, in the words the user reads. Without it, "not applicable"
        // leaves the row saying nothing about what the step expected to find.
        if (Reason(step.Reason) is { } reason)
            fields.Add(
                new RecordFieldViewModel(
                    SymbolRegular.Info24,
                    Loc.Instance["Optimizer.Details.Field.Reason"],
                    reason
                )
            );

        // A step that changed something shows what it moved between. Anything else shows the
        // state it found, because there is no after to speak of.
        if (step.Kind != ChangeKind.Change || !step.HasValuePair)
        {
            var state = StateOf(step);
            var value = step.NewValue ?? step.PreviousValue;

            if (state is { } chip && !string.IsNullOrEmpty(value))
                fields.Add(
                    new RecordFieldViewModel(chip.Icon, Loc.Instance[chip.LabelKey], LocalizeValue(value))
                );

            return fields;
        }

        fields.Add(
            new RecordFieldViewModel(
                SymbolRegular.Clock24,
                Loc.Instance["Optimizer.Details.Field.Previous"],
                step.PreviousValue is null
                    ? Loc.Instance["Optimizer.Details.Field.Previous.None"]
                    : LocalizeValue(step.PreviousValue)
            )
        );

        if (step.NewValue is not null)
            fields.Add(
                new RecordFieldViewModel(
                    SymbolRegular.Edit24,
                    Loc.Instance["Optimizer.Details.Field.New"],
                    LocalizeValue(step.NewValue)
                )
            );

        return fields;
    }

    /// <summary>
    ///     The sentence for a reason a provider recorded. A reason the UI does not know yet is
    ///     left out rather than shown as a code.
    /// </summary>
    private static string? Reason(string? reason)
    {
        return reason switch
        {
            "service.notFound" => Loc.Instance["Optimizer.Details.Reason.ServiceNotFound"],
            "task.notFound" => Loc.Instance["Optimizer.Details.Reason.TaskNotFound"],
            "registry.valueAbsent" => Loc.Instance["Optimizer.Details.Reason.RegistryValueAbsent"],
            "registry.keyExists" => Loc.Instance["Optimizer.Details.Reason.RegistryKeyExists"],
            "registry.keyAbsent" => Loc.Instance["Optimizer.Details.Reason.RegistryKeyAbsent"],
            "usb.noDevices" => Loc.Instance["Optimizer.Details.Reason.UsbNoDevices"],
            _ => null,
        };
    }

    /// <summary>
    ///     The name a provider records is the English one the log uses. The row says the same thing
    ///     in the UI language, and keeps a name it does not know yet as it is, so a new provider
    ///     shows up instead of showing nothing.
    /// </summary>
    private static string ProviderName(string name)
    {
        return name switch
        {
            "Registry" => Loc.Instance["Optimizer.Details.Provider.Registry"],
            "Service" => Loc.Instance["Optimizer.Details.Provider.Service"],
            "Scheduled Task" => Loc.Instance["Optimizer.Details.Provider.ScheduledTask"],
            "PowerPlan" => Loc.Instance["Optimizer.Details.Provider.PowerPlan"],
            "Hibernation" => Loc.Instance["Optimizer.Details.Provider.Hibernation"],
            "USB power" => Loc.Instance["Optimizer.Details.Provider.UsbPower"],
            "Shell" => Loc.Instance["Optimizer.Details.Provider.Shell"],
            _ => name,
        };
    }

    /// <summary>
    ///     A registry value type as Windows itself names it, so the chip reads the same in every
    ///     language and matches what a user sees in the registry editor.
    /// </summary>
    private static string DescribeValueType(string valueType)
    {
        return valueType switch
        {
            "DWord" => "REG_DWORD",
            "QWord" => "REG_QWORD",
            "String" => "REG_SZ",
            "ExpandString" => "REG_EXPAND_SZ",
            "MultiString" => "REG_MULTI_SZ",
            "Binary" => "REG_BINARY",
            _ => valueType,
        };
    }

    private static (string LabelKey, SymbolRegular Icon) SubjectOf(string? operation)
    {
        return operation switch
        {
            "service.startup" => ("Optimizer.Details.Field.Service", SymbolRegular.Shield24),
            "task.enable" or "task.disable" => (
                "Optimizer.Details.Field.Task",
                SymbolRegular.Clock24
            ),
            "power.plan" or "power.planInstall" => (
                "Optimizer.Details.Field.PowerPlan",
                SymbolRegular.BatteryCharge24
            ),
            "power.setting" => ("Optimizer.Details.Field.Setting", SymbolRegular.Gauge24),
            _ => ("Optimizer.Details.Field.Path", SymbolRegular.FolderOpen24),
        };
    }

    /// <summary>
    ///     What the state chip of a step that wrote nothing is called, named after the thing it
    ///     holds. A plan name or a device count is the whole story on its own, so they have none.
    /// </summary>
    private static (string LabelKey, SymbolRegular Icon)? StateOf(ChangeRecordStep step)
    {
        return step.Operation switch
        {
            "service.startup" => ("Optimizer.Details.Field.StartupState", SymbolRegular.Shield24),
            "task.enable" or "task.disable" => (
                "Optimizer.Details.Field.TaskState",
                SymbolRegular.Clock24
            ),
            "hibernation" => (
                "Optimizer.Details.Field.HibernationState",
                SymbolRegular.BatteryCharge24
            ),
            "power.plan" or "power.planInstall" => null,
            _ => ("Optimizer.Details.Field.Value", SymbolRegular.Edit24),
        };
    }

    /// <summary>
    ///     Turns the words a provider records into the words the user reads. A value that is not a
    ///     known word is shown as it is, because a path or a number needs no translation.
    /// </summary>
    private static string LocalizeValue(string value)
    {
        return value switch
        {
            "Automatic" => Loc.Instance["Optimizer.Details.Value.Startup.Automatic"],
            "AutomaticDelayedStart" => Loc.Instance["Optimizer.Details.Value.Startup.AutomaticDelayed"],
            "Manual" => Loc.Instance["Optimizer.Details.Value.Startup.Manual"],
            "Disabled" => Loc.Instance["Optimizer.Details.Value.Startup.Disabled"],
            "Enabled" => Loc.Instance["Optimizer.Details.Value.Enabled"],
            _ => value,
        };
    }
}
