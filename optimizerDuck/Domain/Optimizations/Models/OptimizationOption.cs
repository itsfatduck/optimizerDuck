namespace optimizerDuck.Domain.Optimizations.Models;

/// <summary>Input type for a pre-apply configuration question.</summary>
public enum OptimizationOptionKind
{
    Toggle,
    SingleChoice,
}

/// <summary>One choice inside a <see cref="OptimizationOptionKind.SingleChoice"/> option.</summary>
public sealed record OptimizationOptionChoice(
    string Value,
    string LabelKey,
    string? DescriptionKey = null
);

/// <summary>Describes one pre-apply question exposed by an <see cref="Abstractions.IConfigurableOptimization"/>.</summary>
public sealed record OptimizationOption
{
    public required string Key { get; init; }
    public required OptimizationOptionKind Kind { get; init; }
    public required string LabelKey { get; init; }
    public string? DescriptionKey { get; init; }
    public string? TradeOffKey { get; init; }
    public required string DefaultValue { get; init; }
    public IReadOnlyList<OptimizationOptionChoice> Choices { get; init; } = [];
    public bool IsAlreadyAppliedHint { get; init; }
}

/// <summary>Typed accessors for reading selected option values inside <c>ApplyAsync</c> and probes.</summary>
public static class OptimizationOptionDictionaryExtensions
{
    public static bool GetBool(
        this IReadOnlyDictionary<string, string>? options,
        string key,
        bool fallback = false
    ) =>
        options != null
        && options.TryGetValue(key, out var raw)
        && bool.TryParse(raw, out var parsed)
            ? parsed
            : fallback;

    public static string GetString(
        this IReadOnlyDictionary<string, string>? options,
        string key,
        string fallback = ""
    ) => options != null && options.TryGetValue(key, out var raw) ? raw : fallback;
}
