using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Domain.Execution;

/// <summary>
///     Thread-safe collector of changes made during one apply operation.
///     Providers append here via <see cref="OpCall"/>; the engine derives
///     results and revert data from the collected changes.
/// </summary>
public sealed class ChangeSet
{
    private readonly List<Change> _changes = [];
    private readonly Lock _gate = new();
    private int _sequence;

    /// <summary>All recorded changes in execution order.</summary>
    public IReadOnlyList<Change> Changes
    {
        get
        {
            lock (_gate)
                return _changes.ToList();
        }
    }

    /// <summary>Only the successful changes.</summary>
    public IReadOnlyList<Change> SuccessfulSteps
    {
        get
        {
            lock (_gate)
                return _changes.Where(c => c.Ok).ToList();
        }
    }

    /// <summary>Only the failed changes.</summary>
    public IReadOnlyList<Change> FailedSteps
    {
        get
        {
            lock (_gate)
                return _changes.Where(c => !c.Ok).ToList();
        }
    }

    /// <summary>Whether at least one change succeeded.</summary>
    public bool HasSuccessfulSteps
    {
        get
        {
            lock (_gate)
                return _changes.Any(c => c.Ok);
        }
    }

    /// <summary>Whether at least one change failed.</summary>
    public bool HasFailedSteps
    {
        get
        {
            lock (_gate)
                return _changes.Any(c => !c.Ok);
        }
    }

    /// <summary>
    ///     Whether any step actually modified the system. Skips and irreversible actions
    ///     did not leave something to undo, so they do not count as applied changes. A step that
    ///     modified the system and then failed still counts, because it carries the compensation
    ///     for what it changed, and dropping it here would leave that work unrevertible.
    /// </summary>
    public bool DidApplyAnything
    {
        get
        {
            lock (_gate)
                return _changes.Any(c => c.Kind == ChangeKind.Change && (c.Ok || c.Revert != null));
        }
    }

    /// <summary>Records a step that found nothing to do, so it needs no compensation.</summary>
    public Change AddSkip(string name, string description, ChangeDetail? detail = null) =>
        Add(name, description, true, detail: detail, kind: ChangeKind.Skip);

    /// <summary>
    ///     Records a step whose target does not exist on this machine, so nothing was written
    ///     and no compensation is expected.
    /// </summary>
    public Change AddNotApplicable(string name, string description, ChangeDetail? detail = null) =>
        Add(name, description, true, detail: detail, kind: ChangeKind.NotApplicable);

    /// <summary>
    ///     Records a step Windows refused, so nothing was written and no compensation is
    ///     expected for it.
    /// </summary>
    public Change AddRefused(string name, string description, ChangeDetail? detail = null) =>
        Add(name, description, true, detail: detail, kind: ChangeKind.Refused);

    /// <summary>
    ///     Records a step that modified the system on purpose with no way back, so no
    ///     compensation is expected for it.
    /// </summary>
    public Change AddIrreversible(string name, string description) =>
        Add(name, description, true, kind: ChangeKind.Irreversible);

    /// <summary>Adds a change with an auto-incremented execution index.</summary>
    public Change Add(
        string name,
        string description,
        bool ok,
        IRevertStep? revert = null,
        string? error = null,
        string? errorDetail = null,
        Func<OpCall, Task<OpResult>>? retry = null,
        ChangeKind kind = ChangeKind.Change,
        ChangeDetail? detail = null
    )
    {
        var change = new Change
        {
            Name = name,
            Description = description,
            Ok = ok,
            Kind = kind,
            Revert = revert,
            Error = error,
            ErrorDetail = errorDetail,
            Retry = retry,
            Detail = detail,
        };
        lock (_gate)
        {
            _changes.Add(change with { Index = ++_sequence });
            return _changes[^1];
        }
    }

    /// <summary>
    ///     Maps recorded changes to an apply result. Any success (full or
    ///     partial) is success; total failure carries the first error message.
    ///     Empty is failure (fail-closed): a genuine skip must return
    ///     <see cref="ApplyResult.True"/> directly with a logged reason,
    ///     never an empty set.
    /// </summary>
    public ApplyResult ToApplyResult(string? fallbackError = null)
    {
        lock (_gate)
        {
            // Recording no step at all is a legitimate outcome: an item can match nothing on
            // this machine, and a run whose steps were all skipped still recorded them. A
            // provider that changes something without recording it is caught by the
            // compensation guard in RevertManager, not by failing the user's apply here.
            if (_changes.Count == 0)
                return ApplyResult.True();
            if (_changes.Any(c => c.Ok))
                return ApplyResult.True();
            return ApplyResult.False(
                _changes.FirstOrDefault(c => c.Error != null)?.Error
                    ?? fallbackError
                    ?? Loc.Instance["Optimization.Apply.Error.AllStepsFailed"]
            );
        }
    }
}

/// <summary>
///     A single recorded step: what ran, whether it succeeded, the compensation
///     step for undo, and an optional retry action. <see cref="Index"/> is the
///     1-based execution order, preserved as the revert-file index. The same
///     record is the step result shown to the user.
/// </summary>
public sealed record Change
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public int Index { get; init; }

    public bool Ok { get; init; }

    /// <summary>What the step did, which decides whether compensation data is expected.</summary>
    public ChangeKind Kind { get; init; } = ChangeKind.Change;

    public IRevertStep? Revert { get; init; }

    /// <summary>
    ///     The structured facts of this step, for a UI that speaks the user's language. The log
    ///     keeps using <see cref="Description" />, which is always English.
    /// </summary>
    public ChangeDetail? Detail { get; init; }

    public string? Error { get; init; }

    public string? ErrorDetail { get; init; }

    public Func<OpCall, Task<OpResult>>? Retry { get; init; }
}

/// <summary>
///     What a recorded step did to the system, which decides whether the step is expected
///     to carry the data needed to undo it. Only <see cref="Change" /> modified something.
/// </summary>
public enum ChangeKind
{
    /// <summary>The step modified the system and carries the data needed to undo it.</summary>
    Change,

    /// <summary>The system already had the desired state, so nothing was written.</summary>
    Skip,

    /// <summary>There was nothing on this machine to act on, so nothing was written.</summary>
    NotApplicable,

    /// <summary>Windows refused the change, so nothing was written.</summary>
    Refused,

    /// <summary>The step modified the system on purpose with no way back.</summary>
    Irreversible,
}
