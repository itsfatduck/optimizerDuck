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

    /// <summary>Adds a change with an auto-incremented execution index.</summary>
    public Change Add(
        string name,
        string description,
        bool ok,
        IRevertStep? revert = null,
        string? error = null,
        string? errorDetail = null,
        Func<OpCall, Task<OpResult>>? retry = null
    )
    {
        var change = new Change
        {
            Name = name,
            Description = description,
            Ok = ok,
            Revert = revert,
            Error = error,
            ErrorDetail = errorDetail,
            Retry = retry,
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
            if (_changes.Count == 0)
                return ApplyResult.False(fallbackError ?? Loc.Instance["Revert.Error.NoSteps"]);
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

    public IRevertStep? Revert { get; init; }

    public string? Error { get; init; }

    public string? ErrorDetail { get; init; }

    public Func<OpCall, Task<OpResult>>? Retry { get; init; }
}
