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

    /// <summary>Apply a value to the system.</summary>
    Task ApplyAsync(object? value);

    CustomizeRecommendationResult? GetRecommendation();
}
