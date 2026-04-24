using Microsoft.Extensions.Logging;
using optimizerDuck.Services;

namespace optimizerDuck.Domain.Optimizations.Models;

/// <summary>
///     Provides context information for optimization execution.
/// </summary>
public record OptimizationContext
{
    /// <summary>
    ///     The logger instance used to record diagnostic messages during optimization.
    /// </summary>
    public required ILogger Logger { get; init; }

    /// <summary>
    ///     A snapshot of the current system hardware and software information.
    /// </summary>
    public required SystemSnapshot Snapshot { get; init; }

    /// <summary>
    ///     The service used to download remote resources required by optimizations.
    /// </summary>
    public required StreamService StreamService { get; init; }
}
