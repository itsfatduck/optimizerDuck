using Newtonsoft.Json.Linq;

namespace optimizerDuck.Core.Models.Revert;

/// <summary>
///     Represents a single revert step stored in JSON.
/// </summary>
public class RevertStepData
{
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
    ///     The list of revert steps.
    /// </summary>
    public List<RevertStepData> Steps { get; set; } = new();
}