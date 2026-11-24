using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Abstractions;

/// <summary>
///     Implemented by optimizations that ask the user to choose scope or options before applying.
/// </summary>
public interface IConfigurableOptimization
{
    /// <summary>
    ///     Gets whether the apply button requires at least one toggle to be enabled.
    /// </summary>
    bool RequiresAtLeastOneSelection => false;

    /// <summary>
    ///     Builds the option questions presented before apply, pre-populated from
    ///     <paramref name="previousSelections"/> when reconfiguring or re-applying.
    /// </summary>
    IReadOnlyList<OptimizationOption> GetOptions(
        SystemInfo snapshot,
        IReadOnlyDictionary<string, string>? previousSelections = null
    );
}
