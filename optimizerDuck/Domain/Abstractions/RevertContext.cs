using Microsoft.Extensions.Logging;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Domain.Abstractions;

/// <summary>
/// Per-revert execution context carrying every capability a revert step needs.
/// </summary>
public sealed class RevertContext
{
    public required ShellService Shell { get; init; }

    public required PowerPlanService PowerPlans { get; init; }

    public required ILogger Logger { get; init; }
}
