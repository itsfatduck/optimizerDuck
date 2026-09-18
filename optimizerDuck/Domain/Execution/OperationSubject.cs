namespace optimizerDuck.Domain.Execution;

/// <summary>Identifies the owner of a run, which is an optimization or a built-in tool.</summary>
/// <param name="Id">The stable identifier, which names the run's record file.</param>
/// <param name="Key">The stable key written into the record and the log.</param>
/// <param name="LogName">The English name written to the log.</param>
public sealed record OperationSubject(Guid Id, string Key, string LogName);

/// <summary>Whether a run persists the compensation of the steps it records.</summary>
public enum RevertPersistence
{
    /// <summary>The compensation of every step carrying one is written as revert data.</summary>
    Enabled,

    /// <summary>Nothing is written to the revert store, so the run offers no undo.</summary>
    Disabled,
}
