using Newtonsoft.Json.Linq;

namespace optimizerDuck.Domain.Revert;

/// <summary>
///     Represents a single revert step stored in JSON.
/// </summary>
public class RevertStepData
{
    /// <summary>
    ///     The explicit index of this step in the execution sequence.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     The type of the revert step (e.g., "Registry", "Service", "Shell").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    ///     The serialized data for the step.
    /// </summary>
    public JObject Data { get; set; } = new();
}

/// <summary>
///     Represents the persisted revert data for an optimization.
/// </summary>
public class RevertData
{
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
