using Microsoft.Extensions.Logging;

namespace optimizerDuck.Domain.Execution;

/// <summary>
///     Explicit per-operation call context: every dependency a provider needs
///     (change collector, logger, cancellation) travels as a parameter.
/// </summary>
public class OpCall
{
    public ChangeSet Changes { get; init; } = new();

    public required ILogger Logger { get; init; }

    public CancellationToken CancellationToken { get; init; }
}
