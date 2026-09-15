using System.Diagnostics;
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
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private int _sequence;
    private long _lastRecordedMs;

    /// <summary>When the run that collects these changes started.</summary>
    public DateTime StartedAt { get; } = DateTime.Now;

    /// <summary>How long the run has been going, for the record of what it did.</summary>
    public long ElapsedMs => _clock.ElapsedMilliseconds;

    /// <summary>All recorded changes in execution order.</summary>
    public IReadOnlyList<Change> Changes
    {
        get
        {
            lock (_gate)
                return _changes.ToList();
        }
    }

    /// <summary>Gets the steps that succeeded, in execution order.</summary>
    public IReadOnlyList<Change> SuccessfulSteps
    {
        get
        {
            lock (_gate)
                return _changes.Where(c => c.Ok).ToList();
        }
    }

    /// <summary>Gets the steps that failed, in execution order.</summary>
    public IReadOnlyList<Change> FailedSteps
    {
        get
        {
            lock (_gate)
                return _changes.Where(c => !c.Ok).ToList();
        }
    }

    /// <summary>Gets a value that indicates whether any step succeeded.</summary>
    public bool HasSuccessfulSteps
    {
        get
        {
            lock (_gate)
                return _changes.Any(c => c.Ok);
        }
    }

    /// <summary>Gets a value that indicates whether any step failed.</summary>
    public bool HasFailedSteps
    {
        get
        {
            lock (_gate)
                return _changes.Any(c => !c.Ok);
        }
    }

    /// <summary>
    ///     Whether any step modified the system. Skips and irreversible actions
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
    public Change AddRefused(
        string name,
        string description,
        ChangeDetail? detail = null,
        int? nativeErrorCode = null
    ) =>
        Add(
            name,
            description,
            true,
            detail: detail,
            kind: ChangeKind.Refused,
            nativeErrorCode: nativeErrorCode
        );

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
        ChangeDetail? detail = null,
        int? nativeErrorCode = null
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
            NativeErrorCode = nativeErrorCode,
        };
        lock (_gate)
        {
            // The span since the previous step was recorded, which is the work this step did:
            // a provider records a step as soon as it finishes.
            var elapsedMs = _clock.ElapsedMilliseconds;
            var stepMs = elapsedMs - _lastRecordedMs;
            _lastRecordedMs = elapsedMs;

            _changes.Add(
                change with
                {
                    Index = ++_sequence,
                    ElapsedMs = stepMs,
                    RecordedAt = DateTime.Now,
                }
            );
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
    /// <summary>Gets the name of the operation that recorded the step.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the one-line description of what the step did, always in English.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the 1-based execution order, reused as the step's revert-file index.</summary>
    public int Index { get; init; }

    /// <summary>Gets a value that indicates whether the step succeeded.</summary>
    public bool Ok { get; init; }

    /// <summary>What the step did, which decides whether compensation data is expected.</summary>
    public ChangeKind Kind { get; init; } = ChangeKind.Change;

    /// <summary>
    ///     Gets the compensation step that undoes this step, when it changed something.
    /// </summary>
    public IRevertStep? Revert { get; init; }

    /// <summary>
    ///     The structured facts of this step. The log keeps using <see cref="Description" />,
    ///     which is always English.
    /// </summary>
    public ChangeDetail? Detail { get; init; }

    /// <summary>Gets the message that explains the failure, when the step failed.</summary>
    public string? Error { get; init; }

    /// <summary>Gets the diagnostic detail of the failure, when one was captured.</summary>
    public string? ErrorDetail { get; init; }

    /// <summary>
    ///     The code the failure came from, as the system reported it: a Windows error code for a
    ///     native call, or a process exit code for a program. Null when there was no code, which is
    ///     not the same as zero.
    /// </summary>
    public int? NativeErrorCode { get; init; }

    /// <summary>When the step finished, so a record says when each of its steps ran.</summary>
    public DateTime? RecordedAt { get; init; }

    /// <summary>
    ///     How long the step's own work took, in milliseconds. It is measured as the time since the
    ///     previous step was recorded, so a provider that records its step long after finishing is
    ///     counted into its own duration.
    /// </summary>
    public long? ElapsedMs { get; init; }

    /// <summary>
    ///     Which attempt this step is: 1 for the run that recorded it, one more for each retry that
    ///     took it further.
    /// </summary>
    public int Attempt { get; init; } = 1;

    /// <summary>Gets the action that retries the step, when a retry can help.</summary>
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
