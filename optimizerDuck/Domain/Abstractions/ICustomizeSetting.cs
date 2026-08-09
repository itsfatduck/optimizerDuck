using optimizerDuck.Domain.Customize.Models;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Abstractions;

/// <summary>
///     Defines a single customizable setting that the user can configure.
/// </summary>
public interface ICustomizeSetting
{
    /// <summary>Gets the localized display name of the setting.</summary>
    string Name { get; }

    /// <summary>Gets the localized description of what this setting does.</summary>
    string Description { get; }

    /// <summary>Gets the localized section/group this setting belongs to.</summary>
    string Section { get; }

    /// <summary>Gets the icon symbol displayed next to the setting in the UI.</summary>
    public SymbolRegular Icon { get; }

    /// <summary>Gets the unique key used for localization lookup.</summary>
    string FeatureKey { get; }

    /// <summary>
    ///     Gets the compatibility condition type (implementing <see cref="ICondition"/>)
    ///     that determines whether this setting is supported on the current system.
    /// </summary>
    Type? ConditionType { get; }

    /// <summary>Gets the type of UI control to render for this setting.</summary>
    CustomizeControlType ControlType { get; }

    /// <summary>Gets the matched option value, else the raw registry value.</summary>
    object? CurrentValue { get; }

    /// <summary>
    ///     Gets the options displayed for this setting: the declared options plus, when the
    ///     current registry value falls outside them, a memory-only "Custom" option that
    ///     reflects the actual value. It is derived from the live registry state and never
    ///     persisted.
    /// </summary>
    IReadOnlyList<SettingOption>? Options { get; }

    /// <summary>Read the current system state. For toggles: true = on, false = off.</summary>
    Task<bool> GetStateAsync();

    /// <summary>
    ///     Reads state until two consecutive reads agree or retries run out. Use after
    ///     <see cref="ApplyAsync"/> to let the registry settle.
    /// </summary>
    Task<bool> GetStateWithRetryAsync(int maxRetries = 3, int delayMs = 80);

    /// <summary>Applies a bool (toggles) or a declared dropdown option value.</summary>
    Task ApplyAsync(object? value);

    /// <summary>
    ///     Registry key paths that should be watched for external changes,
    ///     so the UI can auto-refresh when someone else modifies a setting.
    /// </summary>
    IReadOnlyList<string> WatchedRegistryPaths { get; }

    /// <summary>Returns the setting's recommendation, or <c>null</c> when none exists.</summary>
    CustomizeRecommendationResult? GetRecommendation();
}
