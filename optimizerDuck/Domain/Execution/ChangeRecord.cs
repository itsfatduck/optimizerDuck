using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using optimizerDuck.Common.Extensions;
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

    /// <summary>The item's stable English name, so an old record does not carry an old language.</summary>
    public string LogName { get; init; } = string.Empty;

    public DateTime AppliedAt { get; init; }

    /// <summary>Which run this record describes: the last apply, or the revert of it.</summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public ChangeRecordOperation Operation { get; init; } = ChangeRecordOperation.Apply;

    /// <summary>When the item was reverted, for a record that describes a revert.</summary>
    public DateTime? RevertedAt { get; init; }

    /// <summary>The outcome of the run, as the name of the status it ended with.</summary>
    public string Outcome { get; init; } = string.Empty;

    public IReadOnlyList<ChangeRecordStep> Steps { get; init; } = [];

    /// <summary>Counts the steps that ended with one kind and did not fail.</summary>
    public int CountOf(ChangeKind kind) =>
        Steps.Count(step => step.Ok && step.Kind == kind);

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
                .. Steps.Where(step => step.Kind == ChangeKind.Change)
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

    /// <summary>Captures one finished apply. The record is a copy: nothing here is live state.</summary>
    public static ChangeRecord From(ChangeSet changes, IOptimization item, string outcome)
    {
        return new ChangeRecord
        {
            Id = item.Id,
            OptimizationKey = item.OptimizationKey,
            LogName = item.LogName(),
            AppliedAt = DateTime.Now,
            Outcome = outcome,
            Steps =
            [
                .. changes
                    .Changes.OrderBy(change => change.Index)
                    .Select(change => new ChangeRecordStep
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
                    }),
            ],
        };
    }
}

/// <summary>One step of a recorded apply, with the reason it wrote nothing when it did not.</summary>
public sealed record ChangeRecordStep
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    [JsonConverter(typeof(StringEnumConverter))]
    public ChangeKind Kind { get; init; }

    public bool Ok { get; init; } = true;

    public string? Error { get; init; }

    /// <summary>The structured facts of the step, when the provider recorded them.</summary>
    public string? Operation { get; init; }

    public string? Target { get; init; }

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
