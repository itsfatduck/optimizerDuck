using System.Globalization;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Configuration;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Dialogs;

/// <summary>Represents one fact about a step, shown as a chip.</summary>
public sealed record RecordFieldViewModel(SymbolRegular Icon, string Label, string Value);

/// <summary>
///     Represents one recorded step, rebuilt from its structured facts so the row reads in the
///     UI language. The step keeps the English description and values the log uses.
/// </summary>
public sealed class ChangeRecordStepViewModel
{
    public ChangeRecordStepViewModel(
        ChangeRecordStep step,
        ChangeRecordOperation operation = ChangeRecordOperation.Apply
    )
    {
        ArgumentNullException.ThrowIfNull(step);

        Operation = operation;
        Kind = step.Kind;
        Ok = step.Ok;
        Index = step.Index;

        var display = ChangeKindPresentation.ForStep(step);
        Icon = display.Icon;
        KindLabel = Loc.Instance[display.LabelKey];

        Name = ProviderName(step.Name);
        Description = step.Description;
        Detail = step.Error;

        // A step that changed nothing is never phrased as an action, so the row cannot read as
        // if it had written.
        var row = ChangeDetailPresentation.For(
            ChangeDetailCodec.Decode(step),
            step.Kind,
            operation
        );
        OperationLabel = row.ActionKey is null
            ? null
            : Loc.Instance[row.ActionKey, row.ActionArg ?? string.Empty];
        Fields = BuildFields(row, step);
    }

    /// <summary>Builds a row from a change that is still in memory.</summary>
    /// <param name="change">The change to build a row for.</param>
    /// <param name="operation">The run the row belongs to.</param>
    public ChangeRecordStepViewModel(
        Change change,
        ChangeRecordOperation operation = ChangeRecordOperation.Apply
    )
        : this(ChangeRecord.StepOf(change), operation)
    {
        ErrorDetail = change.ErrorDetail;
    }

    /// <summary>Gets the step's position in the run.</summary>
    public int Index { get; }

    /// <summary>Gets the raw failure text.</summary>
    public string? ErrorDetail { get; }

    /// <summary>Gets a value that indicates whether there is raw failure text to show.</summary>
    public bool HasErrorDetail => !string.IsNullOrWhiteSpace(ErrorDetail);

    /// <summary>Gets the run this row belongs to, which decides the wording of the step.</summary>
    public ChangeRecordOperation Operation { get; }

    /// <summary>Gets the kind of step, which decides the accent colour of the row.</summary>
    public ChangeKind Kind { get; }

    /// <summary>
    ///     Gets a value that indicates whether the step succeeded, which decides whether it is
    ///     shown as a failure.
    /// </summary>
    public bool Ok { get; }

    public SymbolRegular Icon { get; }

    public string KindLabel { get; }

    public string Name { get; }

    /// <summary>Gets the English description written for the log.</summary>
    public string Description { get; }

    /// <summary>
    ///     Gets a value that indicates whether the row has nothing but the log text to show. A
    ///     row that states an action or carries facts would only repeat the English log line.
    /// </summary>
    public bool ShowDescription => !HasOperation && !HasFields;

    /// <summary>Gets what the step did in the UI language when it changed something.</summary>
    public string? OperationLabel { get; }

    public bool HasOperation => !string.IsNullOrEmpty(OperationLabel);

    /// <summary>
    ///     Gets the facts of the step: what it touched, the values around it, and how it went.
    /// </summary>
    public IReadOnlyList<RecordFieldViewModel> Fields { get; }

    public bool HasFields => Fields.Count > 0;

    /// <summary>Gets the failure message when the step failed.</summary>
    public string? Detail { get; }

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    /// <summary>
    ///     Builds the facts of the operation, worded for the user, followed by how the step went:
    ///     when it ran, how long it took, which attempt it is and the code behind a failure. A
    ///     fact the record does not carry leaves no chip behind.
    /// </summary>
    private static List<RecordFieldViewModel> BuildFields(
        ChangeDetailRow row,
        ChangeRecordStep step
    )
    {
        var fields = row
            .Chips.Select(chip => new RecordFieldViewModel(
                chip.Icon,
                Loc.Instance[chip.LabelKey],
                chip.ValueIsKey ? Loc.Instance[chip.Value] : chip.Value
            ))
            .ToList();

        if (step.RecordedAt is { } recordedAt)
            fields.Add(
                new RecordFieldViewModel(
                    SymbolRegular.Clock24,
                    Loc.Instance["Optimizer.Details.Field.At"],
                    recordedAt.ToString("T", CultureInfo.CurrentCulture)
                )
            );

        if (step.ElapsedMs is { } elapsedMs)
            fields.Add(
                new RecordFieldViewModel(
                    SymbolRegular.Timer24,
                    Loc.Instance["Optimizer.Details.Field.Took"],
                    DescribeDuration(elapsedMs)
                )
            );

        if (step.Attempt > 1)
            fields.Add(
                new RecordFieldViewModel(
                    SymbolRegular.ArrowClockwise24,
                    Loc.Instance["Optimizer.Details.Field.Attempt"],
                    step.Attempt.ToString(CultureInfo.InvariantCulture)
                )
            );

        if (step.NativeErrorCode is { } errorCode)
            fields.Add(
                new RecordFieldViewModel(
                    SymbolRegular.ErrorCircle24,
                    Loc.Instance["Optimizer.Details.Field.ErrorCode"],
                    errorCode.ToString(CultureInfo.InvariantCulture)
                )
            );

        return fields;
    }

    /// <summary>
    ///     Formats a duration as it is read at a glance: milliseconds while the step was quick,
    ///     and seconds once it was not.
    /// </summary>
    internal static string DescribeDuration(long elapsedMs)
    {
        return elapsedMs < 1000
            ? string.Format(CultureInfo.InvariantCulture, "{0} ms", elapsedMs)
            : string.Format(CultureInfo.InvariantCulture, "{0:0.0} s", elapsedMs / 1000.0);
    }

    /// <summary>
    ///     Translates the English provider name a step records into the UI language, and keeps a
    ///     name it does not know yet, so a new provider still shows up.
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
}
