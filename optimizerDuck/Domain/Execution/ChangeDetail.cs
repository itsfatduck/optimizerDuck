namespace optimizerDuck.Domain.Execution;

/// <summary>
///     The structured facts of one recorded step, so the UI can say what happened in the user's
///     language instead of showing the English text written for the log. Providers fill what they
///     know; a step without a detail keeps its log description on screen.
/// </summary>
public sealed record ChangeDetail
{
    /// <summary>Stable operation name, for example <c>registry.write</c>. The UI localizes it.</summary>
    public string? Operation { get; init; }

    /// <summary>What was acted on: a key path, a service name, a task path.</summary>
    public string? Target { get; init; }

    /// <summary>The name of the value inside the target, when the operation has one.</summary>
    public string? ValueName { get; init; }

    /// <summary>The kind of data written, for example a registry value kind.</summary>
    public string? ValueType { get; init; }

    /// <summary>The value before the step, when there was one. Null means there was none.</summary>
    public string? PreviousValue { get; init; }

    /// <summary>The value after the step, when it wrote one. Null means it removed one.</summary>
    public string? NewValue { get; init; }

    /// <summary>
    ///     Why a step wrote nothing, as a stable code the UI words itself, for example
    ///     <c>service.notFound</c>. Null when there is nothing to explain.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    ///     Whether this step moved between two values. A revert puts the pair back the other way
    ///     round for those steps, and leaves a step that only counts things, like the USB power
    ///     pass, exactly as it is.
    /// </summary>
    public bool HasValuePair { get; init; }
}
