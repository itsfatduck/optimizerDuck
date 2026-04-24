using Newtonsoft.Json.Linq;

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
    ///     Executes this revert step asynchronously.
    /// </summary>
    /// <returns><c>true</c> if the revert succeeded; otherwise, <c>false</c>.</returns>
    Task<bool> ExecuteAsync();

    /// <summary>
    ///     Serializes this revert step to a JSON object for persistence.
    /// </summary>
    /// <returns>A <see cref="JObject" /> containing the serialized step data.</returns>
    JObject ToData();
}
