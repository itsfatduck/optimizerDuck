using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Domain.Abstractions;

/// <summary>
///     Defines a single step that can be executed to revert an optimization.
/// </summary>
public interface IRevertStep
{
    /// <summary>
    ///     The type identifier for this revert step (e.g., "Registry", "Service", "Shell").
    /// </summary>
    public string Type { get; }

    /// <summary>
    ///     A localized description of what this revert step does.
    /// </summary>
    public string Description { get; }

    /// <summary>
    ///     Executes this revert step with its full execution context.
    /// </summary>
    /// <param name="context">Shell, power service, and logger for this revert.</param>
    /// <param name="logger">The logger to record provider and shell operations.</param>
    /// <returns>
    ///     <see langword="true" /> if the revert succeeded; otherwise <see langword="false" />.
    /// </returns>
    Task<bool> ExecuteAsync(RevertContext context, ILogger logger);

    /// <summary>
    ///     Serializes this revert step to a JSON object for persistence.
    /// </summary>
    /// <returns>A <see cref="JObject" /> containing the serialized step data.</returns>
    JObject ToData();
}
