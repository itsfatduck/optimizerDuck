using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Services;

namespace optimizerDuck.Core.Models.Optimization;

public record OptimizationContext(
    ILogger Logger,
    SystemSnapshot Snapshot,
    StreamService StreamService);