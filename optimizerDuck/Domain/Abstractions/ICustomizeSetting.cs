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

    /// <summary>Gets the type of UI control to render for this setting.</summary>
    CustomizeControlType ControlType { get; }

    object? CurrentValue { get; }

    IReadOnlyList<SettingOption>? Options { get; }

    /// <summary>Read the current system state. For toggles: true = on, false = off.</summary>
    Task<bool> GetStateAsync();

    /// <summary>
    /// Reads state with a cooldown and convergence check. Use after
    /// <see cref="ApplyAsync"/> to let the registry settle.
    /// </summary>
    Task<bool> GetStateWithRetryAsync(int maxRetries = 3, int delayMs = 80);

    Task ApplyAsync(object? value);

    /// <summary>
    /// Registry key paths that should be watched for external changes,
    /// so the UI can auto-refresh when someone else modifies a setting.
    /// </summary>
    IReadOnlyList<string> WatchedRegistryPaths { get; }

    CustomizeRecommendationResult? GetRecommendation();
}
