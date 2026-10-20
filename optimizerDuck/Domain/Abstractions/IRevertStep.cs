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
    /// <summary>Gets the type identifier for this revert step.</summary>
    string Type { get; }

    /// <summary>Gets the localized description of what this revert step does.</summary>
    string Description { get; }

    /// <summary>
    ///     Executes this revert step with its full execution context.
    /// </summary>
    /// <param name="context">The execution context carrying shell, power plan, and logging services.</param>
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
