using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Domain.Conditions;

/// <summary>
///     The outcome of evaluating a condition against the current system. Localized
///     title/description are resolved lazily at read time, so the current culture wins.
/// </summary>
/// <remarks>
///     A plain class (reference equality), not a record: a fresh instance per evaluation
///     lets view models observe change notifications even when the state is unchanged.
/// </remarks>
public sealed class ConditionResult
{
    /// <summary>
    ///     Creates a condition result with the given state and optional localized
    ///     title/description providers.
    /// </summary>
    /// <param name="state">The outcome state of the check.</param>
    /// <param name="titleProvider">Provider for the condition title, or <c>null</c>.</param>
    /// <param name="descriptionProvider">Provider for the condition explanation, or <c>null</c>.</param>
    public ConditionResult(
        ConditionState state,
        Func<string>? titleProvider = null,
        Func<string>? descriptionProvider = null
    )
    {
        State = state;
        TitleProvider = titleProvider;
        DescriptionProvider = descriptionProvider;
    }

    /// <summary>A convenience result representing a fully supported item.</summary>
    public static readonly ConditionResult Available = new(ConditionState.Available);

    /// <summary>Creates an "unsupported" result with the given localized title/description.</summary>
    public static ConditionResult Unsupported(Func<string> title, Func<string> description) =>
        new(ConditionState.Unsupported, title, description);

    /// <summary>
    ///     Creates an "error" result. Evaluation errors do not block the item: a failure
    ///     never hides an item the user could still use.
    /// </summary>
    public static ConditionResult Error() =>
        new(
            ConditionState.Error,
            () => Loc.Instance["Condition.Error.Title"],
            () => Loc.Instance["Condition.Error.Description"]
        );

    /// <summary>Gets the outcome state of the check.</summary>
    public ConditionState State { get; }

    /// <summary>Gets the provider for the condition title, or <c>null</c>.</summary>
    public Func<string>? TitleProvider { get; }

    /// <summary>Gets the provider for the condition explanation, or <c>null</c>.</summary>
    public Func<string>? DescriptionProvider { get; }

    /// <summary>
    ///     Gets whether this result should block the item by default. Only
    ///     <see cref="ConditionState.Unsupported"/> blocks.
    /// </summary>
    public bool IsBlocking => State is ConditionState.Unsupported;

    /// <summary>Gets the localized title for this result, or <c>null</c>.</summary>
    public string? Title => TitleProvider?.Invoke();

    /// <summary>Gets the localized description for this result, or <c>null</c>.</summary>
    public string? Description => DescriptionProvider?.Invoke();
}
