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
    SymbolRegular Icon { get; }

    /// <summary>Gets the unique key used for localization lookup.</summary>
    string FeatureKey { get; }

    /// <summary>
    ///     Gets the compatibility condition type (implementing <see cref="ICondition" />)
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

    /// <summary>Reads the current system state.</summary>
    /// <returns><see langword="true" /> if the setting is active; otherwise, <see langword="false" />.</returns>
    Task<bool> GetStateAsync();

    /// <summary>
    ///     Reads state until two consecutive reads agree or retries run out.
    /// </summary>
    /// <remarks>
    ///     Used after <see cref="ApplyAsync" /> to let the registry settle.
    /// </remarks>
    /// <param name="maxRetries">The maximum number of read attempts.</param>
    /// <param name="delayMs">The delay in milliseconds between read attempts.</param>
    /// <returns><see langword="true" /> if the setting is active; otherwise, <see langword="false" />.</returns>
    Task<bool> GetStateWithRetryAsync(int maxRetries = 3, int delayMs = 80);

    /// <summary>
    ///     Applies the setting value, recording writes for change tracking and revert.
    /// </summary>
    /// <param name="value">The setting value to apply.</param>
    /// <param name="call">The per-operation context used to record changes.</param>
    /// <returns>The result of the apply operation.</returns>
    Task<Execution.OpResult> ApplyAsync(object? value, Execution.OpCall call);

    /// <summary>
    ///     Gets the registry key paths to monitor for external changes.
    /// </summary>
    IReadOnlyList<string> WatchedRegistryPaths { get; }

    /// <summary>
    ///     Gets the recommendation for this setting, or <see langword="null" /> if none exists.
    /// </summary>
    /// <returns>A <see cref="CustomizeRecommendationResult" />, or <see langword="null" />.</returns>
    CustomizeRecommendationResult? GetRecommendation();
}
