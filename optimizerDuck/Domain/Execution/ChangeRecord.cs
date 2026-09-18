using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;

namespace optimizerDuck.Domain.Execution;

/// <summary>
///     What one apply did, written so it can be shown again after the app restarts. This is not
///     revert data: nothing here is used to undo anything, and a revert does not delete it.
/// </summary>
public sealed record ChangeRecord
{
    public Guid Id { get; init; }

    public string OptimizationKey { get; init; } = string.Empty;

    /// <summary>The item's stable English name, so a record carries no stale language.</summary>
    public string LogName { get; init; } = string.Empty;

    public DateTime AppliedAt { get; init; }

    /// <summary>When the run started, and how long it took, so the record shows its cost.</summary>
    public DateTime? StartedAt { get; init; }

    public long? ElapsedMs { get; init; }

    /// <summary>The build that wrote this record, so a report names what produced it.</summary>
    public string? AppVersion { get; init; }

    /// <summary>The Windows version the run happened on.</summary>
    public string? WindowsVersion { get; init; }

    /// <summary>Which run this record describes: the last apply, or the revert of it.</summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public ChangeRecordOperation Operation { get; init; } = ChangeRecordOperation.Apply;

    /// <summary>When the item was reverted, for a record that describes a revert.</summary>
    public DateTime? RevertedAt { get; init; }

    /// <summary>The outcome of the run, as the name of the status it ended with.</summary>
    public string Outcome { get; init; } = string.Empty;

    public IReadOnlyList<ChangeRecordStep> Steps { get; init; } = [];

    /// <summary>Counts the steps that ended with one kind and did not fail.</summary>
    public int CountOf(ChangeKind kind) => Steps.Count(step => step.Ok && step.Kind == kind);

    public int ChangedCount => CountOf(ChangeKind.Change);

    public int FailedCount => Steps.Count(step => !step.Ok);

    /// <summary>
    ///     The same record, describing the revert of the run it holds. Only the steps that changed
    ///     something were undone, and for each of them the before and the after swap places: a
    ///     revert puts back what the apply replaced, so what the apply wrote is now the previous
    ///     value and what it replaced is now the new one.
    /// </summary>
    /// <param name="at">When the revert ran.</param>
    /// <returns>The record as the revert leaves it.</returns>
    public ChangeRecord Reverted(DateTime at)
    {
        return this with
        {
            Operation = ChangeRecordOperation.Revert,
            RevertedAt = at,
            Steps =
            [
                .. Steps
                    .Where(step => step.Kind == ChangeKind.Change)
                    .Select(step =>
                        step.HasValuePair
                            ? step with
                            {
                                PreviousValue = step.NewValue,
                                NewValue = step.PreviousValue,
                            }
                            : step
                    ),
            ],
        };
    }

    /// <summary>Captures one finished run.</summary>
    /// <param name="changes">The steps the run recorded.</param>
    /// <param name="subject">The owner the record belongs to.</param>
    /// <param name="outcome">The name of the status the run ended with.</param>
    /// <returns>The record as the file stores it.</returns>
    public static ChangeRecord From(ChangeSet changes, OperationSubject subject, string outcome)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(subject);

        return new ChangeRecord
        {
            Id = subject.Id,
            OptimizationKey = subject.Key,
            LogName = subject.LogName,
            AppliedAt = DateTime.Now,
            StartedAt = changes.StartedAt,
            ElapsedMs = changes.ElapsedMs,
            AppVersion = Shared.FileVersion,
            WindowsVersion = Environment.OSVersion.Version.ToString(),
            Outcome = outcome,
            Steps = [.. changes.Changes.OrderBy(change => change.Index).Select(StepOf)],
        };
    }

    /// <summary>Captures one finished apply for an optimization.</summary>
    /// <param name="changes">The steps the run recorded.</param>
    /// <param name="item">The optimization the record belongs to.</param>
    /// <param name="outcome">The name of the status the run ended with.</param>
    /// <returns>The record as the file stores it.</returns>
    public static ChangeRecord From(ChangeSet changes, IOptimization item, string outcome)
    {
        ArgumentNullException.ThrowIfNull(item);

        return From(
            changes,
            new OperationSubject(item.Id, item.OptimizationKey, item.LogName()),
            outcome
        );
    }

    /// <summary>
    ///     The same record as a retry leaves it: a step the retry took further takes the
    ///     outcome the retry reached and counts one more attempt, and a step the retry did
    ///     not touch keeps what it had. What the retry wrote is what the record then says,
    ///     so the history shows a step that needed a second attempt, not one that once failed.
    /// </summary>
    /// <param name="recovered">The steps the retry recovered.</param>
    /// <param name="stillFailed">The steps that failed again.</param>
    /// <returns>The record as the retry leaves it.</returns>
    public ChangeRecord Retried(IReadOnlyList<Change> recovered, IReadOnlyList<Change> stillFailed)
    {
        ArgumentNullException.ThrowIfNull(recovered);
        ArgumentNullException.ThrowIfNull(stillFailed);

        var retried = new Dictionary<int, Change>();
        foreach (var change in recovered)
            retried[change.Index] = change;
        foreach (var change in stillFailed)
            retried[change.Index] = change;

        if (retried.Count == 0)
            return this;

        return this with
        {
            Steps =
            [
                .. Steps.Select(step =>
                    retried.TryGetValue(step.Index, out var change) ? StepOf(change) : step
                ),
            ],
        };
    }

    /// <summary>
    ///     One recorded step as the file stores it. The facts of the operation go through the
    ///     codec, which is the only thing that knows how a detail is laid out.
    /// </summary>
    internal static ChangeRecordStep StepOf(Change change) =>
        new ChangeRecordStep
        {
            Index = change.Index,
            Name = change.Name,
            Description = change.Description,
            Kind = change.Kind,
            Ok = change.Ok,
            Error = change.Error,
            NativeErrorCode = change.NativeErrorCode,
            RecordedAt = change.RecordedAt,
            ElapsedMs = change.ElapsedMs,
            Attempt = change.Attempt,
        }.WithDetail(change.Detail);
}

/// <summary>One step of a recorded apply, carrying the reason it wrote nothing.</summary>
public sealed record ChangeRecordStep
{
    /// <summary>
    ///     The step's position in the run, kept as the revert file keeps it, so a record that was
    ///     rewritten after a retry can still match a step to the attempt that changed it.
    /// </summary>
    public int Index { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    [JsonConverter(typeof(StringEnumConverter))]
    public ChangeKind Kind { get; init; }

    public bool Ok { get; init; } = true;

    public string? Error { get; init; }

    /// <summary>
    ///     The code the failure came from, as the system reported it. Null when there was no code,
    ///     which is not the same as zero.
    /// </summary>
    public int? NativeErrorCode { get; init; }

    /// <summary>When the step finished, and how long its own work took.</summary>
    public DateTime? RecordedAt { get; init; }

    public long? ElapsedMs { get; init; }

    /// <summary>Which attempt this step is: 1, or one more for each retry.</summary>
    public int Attempt { get; init; } = 1;

    /// <summary>The structured facts of the step, when the provider recorded them.</summary>
    public string? Operation { get; init; }

    public string? Target { get; init; }

    /// <summary>
    ///     How the target is named to the user when Windows can name it, for example a power
    ///     setting's name. Null when the target's identifier is all there is to show.
    /// </summary>
    public string? DisplayName { get; init; }

    public string? ValueName { get; init; }

    public string? ValueType { get; init; }

    public string? PreviousValue { get; init; }

    public string? NewValue { get; init; }

    /// <summary>Whether the values above are a before and an after, which a revert swaps.</summary>
    public bool HasValuePair { get; init; }

    /// <summary>Why the step wrote nothing, as a code the UI words itself.</summary>
    public string? Reason { get; init; }

    /// <summary>Whether this step carries anything a localized row can be built from.</summary>
    public bool HasDetail => !string.IsNullOrEmpty(Operation) || !string.IsNullOrEmpty(Target);
}

/// <summary>Which run a record describes.</summary>
public enum ChangeRecordOperation
{
    Apply,

    Revert,
}
