using System.Globalization;
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

        // Only a step that changed something is phrased as an action. A step that wrote nothing
        // states what it found instead, so the row can never read as if it had written.
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

    /// <summary>Builds a row straight from a step that is still in memory, for the failure list.</summary>
    /// <param name="change">The recorded step.</param>
    public ChangeRecordStepViewModel(
        Change change,
        ChangeRecordOperation operation = ChangeRecordOperation.Apply
    )
        : this(ChangeRecord.StepOf(change), operation)
    {
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

    /// <summary>The facts of the step: what it touched, the values around it, and how it went.</summary>
    public IReadOnlyList<RecordFieldViewModel> Fields { get; }

    public bool HasFields => Fields.Count > 0;

    /// <summary>The failure message, when the step failed.</summary>
    public string? Detail { get; }

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    /// <summary>
    ///     The facts of the operation, worded for the user, followed by how the step went: when it
    ///     ran, how long it took, which attempt it is and the code behind a failure. A fact the
    ///     record does not carry leaves its chip out rather than showing an empty one.
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
    ///     A duration as it is read at a glance: milliseconds while the step was quick, and seconds
    ///     once it was not. Shared with the record's own facts, so a run and its steps read alike.
    /// </summary>
    internal static string DescribeDuration(long elapsedMs)
    {
        return elapsedMs < 1000
            ? string.Format(CultureInfo.InvariantCulture, "{0} ms", elapsedMs)
            : string.Format(CultureInfo.InvariantCulture, "{0:0.0} s", elapsedMs / 1000.0);
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
}
