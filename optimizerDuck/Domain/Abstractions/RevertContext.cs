using Microsoft.Extensions.Logging;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Domain.Abstractions;

/// <summary>
/// Per-revert execution context carrying every capability a revert step needs.
/// </summary>
public sealed class RevertContext
{
    /// <summary>Gets the shell execution service used to run shell-based compensation commands.</summary>
    public required ShellService Shell { get; init; }

    /// <summary>Gets the power plan service used to inspect and restore power schemes.</summary>
    public required PowerPlanService PowerPlans { get; init; }

    /// <summary>Gets the logger for recording provider diagnostic details during revert execution.</summary>
    public required ILogger Logger { get; init; }
}
