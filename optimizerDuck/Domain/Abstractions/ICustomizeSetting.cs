using optimizerDuck.Domain.Customize.Models;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Abstractions;

public interface ICustomizeSetting
{
    string Name { get; }
    string Description { get; }
    string Section { get; }
    public SymbolRegular Icon { get; }
    string FeatureKey { get; }

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

    /// <summary>Apply a value to the system.</summary>
    Task ApplyAsync(object? value);

    /// <summary>
    /// Registry key paths that should be watched for external changes,
    /// so the UI can auto-refresh when someone else modifies a setting.
    /// </summary>
    IReadOnlyList<string> WatchedRegistryPaths { get; }

    CustomizeRecommendationResult? GetRecommendation();
}
