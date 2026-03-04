using optimizerDuck.Core.Models.UI;

namespace optimizerDuck.Core.Models.Attributes;

/// <summary>
///     Marks a class as an optimization and provides metadata used for registration and display.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class OptimizationAttribute : Attribute
{
    /// <summary>
    ///     The unique identifier (GUID) for this optimization, used for tracking applied state.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    ///     The risk level associated with applying this optimization.
    /// </summary>
    public required OptimizationRisk Risk { get; init; }

    /// <summary>
    ///     The tags that categorize the system areas this optimization affects.
    /// </summary>
    public required OptimizationTags Tags { get; init; }
}