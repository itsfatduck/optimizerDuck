using Microsoft.Extensions.Logging;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Domain.Abstractions;

/// <summary>
/// Explicit per-revert execution context: every capability a revert step
/// needs travels as a parameter instead of hiding behind service locators.
/// </summary>
public sealed class RevertContext
{
    public required ShellService Shell { get; init; }

    public required PowerPlanService PowerPlans { get; init; }

    public required ILogger Logger { get; init; }
}
