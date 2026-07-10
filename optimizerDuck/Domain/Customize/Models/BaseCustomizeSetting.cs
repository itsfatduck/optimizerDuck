using System.Reflection;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Services.Configuration;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Customize.Models;

/// <summary>
///     Base class for customize settings that read from and write to the Windows registry.
///     Subclasses declare <see cref="RegistryToggles"/> to define which registry values
///     are controlled, and may override <see cref="RefreshScope"/> to specify which
///     Windows surfaces should be refreshed after applying.
/// </summary>
public abstract class BaseCustomizeSetting : ICustomizeSetting
{
    private CustomizeSettingAttribute? _meta;

    private CustomizeSettingAttribute Meta =>
        _meta ??=
            GetType().GetCustomAttribute<CustomizeSettingAttribute>()
            ?? throw new InvalidOperationException(
                $"{GetType().Name} is missing [CustomizeSetting] attribute"
            );

    public Type? OwnerType { get; set; }

    public string OwnerKey =>
        OwnerType?.Name
        ?? throw new InvalidOperationException($"{GetType().Name} has no owner assigned");

    public string FeatureKey => GetType().Name;
    public SymbolRegular Icon => Meta.Icon;

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

    public virtual CustomizeControlType ControlType => CustomizeControlType.Toggle;
    public virtual object? CurrentValue => null;
    public virtual IReadOnlyList<SettingOption>? Options => null;

    public virtual Task<bool> GetStateAsync()
    {
        return Task.Run(() =>
        {
            var toggles = RegistryToggles.ToList();
            if (toggles.Count == 0)
                return false;

            var required = toggles.Where(t => !t.IsOptional).ToList();
            if (required.Count == 0)
                required = toggles;

            return required.All(t => t.GetState());
        });
    }

    public async Task<bool> GetStateWithRetryAsync(int maxRetries = 3, int delayMs = 80)
    {
        bool? previous = null;

        for (var i = 0; i < maxRetries; i++)
        {
            if (i > 0)
                await Task.Delay(delayMs);

            var state = await GetStateAsync();

            if (previous.HasValue && previous.Value == state)
                return state;

            previous = state;
        }

        return previous ?? await GetStateAsync();
    }

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

    protected virtual IEnumerable<RegistryToggle> RegistryToggles => [];

    IReadOnlyList<string> ICustomizeSetting.WatchedRegistryPaths => GetWatchedRegistryPaths();

    protected virtual IReadOnlyList<string> GetWatchedRegistryPaths() =>
        [.. RegistryToggles.Select(t => t.Path).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Whether the setting requires a Windows refresh after <see cref="ApplyAsync"/>.
    /// Defaults to <c>false</c>; auto-derived from <see cref="RefreshScope"/> but
    /// can be overridden for custom behaviour.
    /// </summary>
    protected virtual bool NeedsPostAction => RefreshScope != CustomizeRefreshScope.None;

    /// <summary>
    /// Granular set of Windows surfaces that must be notified after
    /// <see cref="ApplyAsync"/>. Override this to declare exactly which
    /// refresh strategies are required (e.g. <see cref="CustomizeRefreshScope.DesktopIcons"/>
    /// for settings that affect the desktop icon list). Default is
    /// <see cref="CustomizeRefreshScope.None"/> - opt in by overriding.
    /// </summary>
    protected virtual CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.None;

    /// <summary>
    /// Runs every refresh strategy declared in <see cref="RefreshScope"/>.
    /// Subclasses can override this to add custom Win32 work alongside or
    /// instead of the default refresh pipeline.
    /// </summary>
    protected virtual async Task ExecutePostActionAsync()
    {
        var scope = RefreshScope;
        if (scope == CustomizeRefreshScope.None)
            return;

        await Task.Run(() =>
        {
            if (scope.HasFlag(CustomizeRefreshScope.Settings))
                SystemRefreshService.NotifySettingChange();
            if (scope.HasFlag(CustomizeRefreshScope.Associations))
                SystemRefreshService.RefreshShell();
            if (scope.HasFlag(CustomizeRefreshScope.Desktop))
                SystemRefreshService.RefreshDesktop();
            if (scope.HasFlag(CustomizeRefreshScope.DesktopIconCache))
                SystemRefreshService.RefreshDesktopIconVisibilityFromRegistry();
            if (scope.HasFlag(CustomizeRefreshScope.Taskbar))
                SystemRefreshService.NotifyTaskbarSettingChange();
            if (scope.HasFlag(CustomizeRefreshScope.PolicyUpdate))
                SystemRefreshService.UpdatePerUserSystemParameters();
            if (scope.HasFlag(CustomizeRefreshScope.Theme))
                SystemRefreshService.NotifyThemeChanged();
        });
    }

    protected SettingOption Option(string optionKey, object value) =>
        new(Loc.Instance[$"Customize.{OwnerKey}.{FeatureKey}.Options.{optionKey}"], value);

    protected string RecommendationPrefix => $"Customize.{OwnerKey}.{FeatureKey}.Recommendation";

    public virtual CustomizeRecommendationResult? GetRecommendation()
    {
        var state = Meta.Recommendation;
        if (state == RecommendationState.None)
            return null;

        return new CustomizeRecommendationResult(state, $"{RecommendationPrefix}.Reason");
    }
}
