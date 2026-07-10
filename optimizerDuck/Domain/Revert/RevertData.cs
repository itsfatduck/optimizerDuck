using Newtonsoft.Json.Linq;

namespace optimizerDuck.Domain.Revert;

/// <summary>
///     Represents a single revert step stored in JSON.
/// </summary>
public class RevertStepData
{
    /// <summary>
    ///     Gets or sets the explicit 1-based index of this step in the execution sequence.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     Gets or sets the type of the revert step (e.g., "Registry", "Service", "Shell").
    ///     Used for deserialization of the correct step type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the serialized JSON data for the revert step.
    ///     The structure of this object depends on <see cref="Type"/>.
    /// </summary>
    public JObject Data { get; set; } = new();
}

/// <summary>
///     Represents the persisted revert data for an optimization.
/// </summary>
public class RevertData
{
    /// <summary>
    ///     Schema version for forward-compatible migrations. Current version is <c>1</c>.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    ///     The unique identifier of the optimization.
    /// </summary>
    public Guid OptimizationId { get; set; }

    /// <summary>
    ///     The name of the optimization.
    /// </summary>
    public string OptimizationName { get; set; } = string.Empty;

    /// <summary>
    ///     When the optimization was applied.
    /// </summary>
    public DateTime AppliedAt { get; set; }

    /// <summary>
    ///     The array of revert steps. Gaps are represented as null entries.
    ///     Example: [step1, null, null, step4] for steps at indexes 1 and 4.
    /// </summary>
    public RevertStepData?[] Steps { get; set; } = Array.Empty<RevertStepData?>();
}
