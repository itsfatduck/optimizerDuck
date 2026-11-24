using Microsoft.Extensions.Logging;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Domain.Optimizations.Models;

/// <summary>
///     Per-apply call context: the explicit <see cref="OpCall"/> surface
///     (change collector, logger, cancellation) plus optimization-specific
///     dependencies (system snapshot, download service).
/// </summary>
public class OptimizationContext : OpCall
{
    public static readonly IReadOnlyDictionary<string, string> EmptyOptions =
        new Dictionary<string, string>();

    /// <summary>Gets the system snapshot containing hardware, OS, and driver details.</summary>
    public required SystemInfo Snapshot { get; init; }

    /// <summary>
    ///     Gets the service used to download remote resources required by optimizations.
    /// </summary>
    public required StreamService StreamService { get; init; }

    /// <summary>
    ///     Gets the shell service used to execute CMD/PowerShell commands.
    /// </summary>
    public required ShellService Shell { get; init; }

    /// <summary>
    ///     Gets the native power-scheme service (get/set/import/delete, live names).
    /// </summary>
    public required PowerPlanService PowerPlans { get; init; }

    /// <summary>Gets the pre-apply options selected by the user, if any.</summary>
    public IReadOnlyDictionary<string, string> Options { get; init; } = EmptyOptions;
}
