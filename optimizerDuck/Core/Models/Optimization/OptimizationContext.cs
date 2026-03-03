using Microsoft.Extensions.Logging;
using optimizerDuck.Services;

namespace optimizerDuck.Core.Models.Optimization;

/// <summary>
///     Provides context information for optimization execution.
/// </summary>
public record OptimizationContext(
    ILogger Logger,
    SystemSnapshot Snapshot,
    StreamService StreamService);