using System.Reflection;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Services.Managers;
using optimizerDuck.Services.Optimization.Providers;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Customize.Models;

public abstract class BaseCustomizeSetting : ICustomizeSetting
{
    private CustomizeSettingAttribute? _meta;

    private CustomizeSettingAttribute Meta =>
        _meta ??=
            GetType().GetCustomAttribute<CustomizeSettingAttribute>()
            ?? throw new InvalidOperationException(
                $"{GetType().Name} is missing [CustomizeSetting] attribute"
            );

    #region Metadata & Identity

    public Type? OwnerType { get; set; }

    public string OwnerKey =>
        OwnerType?.Name
        ?? throw new InvalidOperationException($"{GetType().Name} has no owner assigned");

    public string FeatureKey => GetType().Name;
    public SymbolRegular Icon => Meta.Icon;

    #endregion

    #region Localization

    public string Name => Loc.Instance[$"Customize.{OwnerKey}.{FeatureKey}.Name"];
    public string Description => Loc.Instance[$"Customize.{OwnerKey}.{FeatureKey}.Description"];

    public string Section
    {
        get
        {
            var section = Meta.GetSectionName();
            return string.IsNullOrEmpty(section)
                ? string.Empty
                : Loc.Instance[$"Customize.{OwnerKey}.Section.{section}"];
        }
    }

    #endregion

    #region Control Configuration

    /// <summary>Which UI control to render. Override for non-toggle types.</summary>
    public virtual CustomizeControlType ControlType => CustomizeControlType.Toggle;

    /// <summary>Current value for Dropdown / Option / Numeric / String controls.</summary>
    public virtual object? CurrentValue => null;

    /// <summary>Available choices for Dropdown / Option controls.</summary>
    public virtual IReadOnlyList<SettingOption>? Options => null;

    #endregion

    #region State Management

    /// <summary>
    /// Read the current system state. For toggles: true = on, false = off.
    /// Default implementation checks all <see cref="RegistryToggles"/>.
    /// </summary>
    public virtual Task<bool> GetStateAsync()
    {
        var toggles = RegistryToggles.ToList();
        if (toggles.Count == 0)
            return Task.FromResult(false);

        var required = toggles.Where(t => !t.IsOptional).ToList();
        if (required.Count == 0)
            required = toggles;

        return Task.FromResult(required.All(t => t.GetState()));
    }

    /// <summary>
    /// Apply a value. The UI calls this on every user interaction
    /// <para/>
    /// Default: if <paramref name="value"/> is <c>bool</c>, writes all
    /// <see cref="RegistryToggles"/> and runs the post-action.
    /// Override for custom behaviour.
    /// </summary>
    public virtual async Task ApplyAsync(object? value)
    {
        if (value is bool isOn)
        {
            await Task.Run(() =>
            {
                foreach (var toggle in RegistryToggles)
                    toggle.SetState(isOn);
            });
        }

        if (NeedsPostAction)
            await ExecutePostActionAsync();
    }

    /// <summary>
    /// Optional: override to provide registry key/value pairs that back a toggle.
    /// Used by the default <see cref="GetStateAsync"/> and <see cref="ApplyAsync"/>.
    /// Leave empty for non-toggle items.
    /// </summary>
    protected virtual IEnumerable<RegistryToggle> RegistryToggles => [];

    protected virtual bool NeedsPostAction => false;

    protected virtual async Task ExecutePostActionAsync()
    {
        await ShellService.CMDAsync("taskkill /f /im explorer.exe & start explorer.exe");
    }

    #endregion

    #region Recommendation

    /// <summary>
    /// Creates a <see cref="SettingOption"/> whose <see cref="SettingOption.DisplayName"/>
    /// is resolved from <c>"Customize.{OwnerKey}.{FeatureKey}.Options.{optionKey}"</c>.
    /// </summary>
    protected SettingOption Option(string optionKey, object value) =>
        new(Loc.Instance[$"Customize.{OwnerKey}.{FeatureKey}.Options.{optionKey}"], value);

    public string RecommendationPrefix => $"Customize.{OwnerKey}.{FeatureKey}.Recommendation";

    public virtual CustomizeRecommendationResult? GetRecommendation()
    {
        var state = Meta.Recommendation;
        if (state == RecommendationState.None)
            return null;

        return new CustomizeRecommendationResult(state, $"{RecommendationPrefix}.Reason");
    }

    #endregion
}
