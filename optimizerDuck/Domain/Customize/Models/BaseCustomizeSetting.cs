using System.Reflection;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.Optimization.Providers;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Customize.Models;

/// <summary>
///     Base class for customize settings that read from and write to the Windows registry.
///     Subclasses declare <see cref="RegistryToggles"/> to define which registry values
///     are controlled, and may override <see cref="RefreshScope"/> to specify which
///     Windows surfaces should be refreshed after applying.
///     For Dropdown settings, options can carry <see cref="RegistryBinding"/> to auto-read/write.
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

    public virtual object? CurrentValue
    {
        get
        {
            var options = GetOptions();
            if (ControlType != CustomizeControlType.Dropdown || options == null)
                return null;

            foreach (var option in options)
            {
                if (option.Bindings is not { Count: > 0 })
                    continue;

                var allMatch = option.Bindings.All(b =>
                {
                    var actual = RegistryService.Read<object>(new RegistryItem(b.Path, b.Name));
                    return ValuesEqual(actual, b.Value);
                });

                if (allMatch)
                    return option.Value;
            }

            return ReadPrimaryRawValue(options);
        }
    }

    public virtual IReadOnlyList<SettingOption>? Options => GetOptions();

    protected virtual IReadOnlyList<SettingOption>? GetOptions() => null;

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
        else if (ControlType == CustomizeControlType.Dropdown && Options != null)
        {
            // Find matching option and apply all its bindings
            var option = Options.FirstOrDefault(o => Equals(o.Value, value));
            if (option?.Bindings is { Count: > 0 })
            {
                foreach (var binding in option.Bindings)
                {
                    if (binding.Value == null)
                        RegistryService.DeleteValue(new RegistryItem(binding.Path, binding.Name));
                    else
                        RegistryService.Write(binding.ToRegistryItem());
                }
            }
        }

        if (NeedsPostAction)
            await ExecutePostActionAsync();
    }

    protected virtual IEnumerable<RegistryToggle> RegistryToggles => [];

    IReadOnlyList<string> ICustomizeSetting.WatchedRegistryPaths => GetWatchedRegistryPaths();

    protected virtual IReadOnlyList<string> GetWatchedRegistryPaths()
    {
        // From RegistryToggles
        var fromToggles = RegistryToggles.Select(t => t.Path);

        // From ALL Dropdown option bindings (not just primary)
        var fromOptions =
            ControlType == CustomizeControlType.Dropdown && Options != null
                ? Options.Where(o => o.Bindings != null)
                    .SelectMany(o => o.Bindings!)
                    .Select(b => b.Path)
                : [];

        return [
            .. fromToggles
                .Concat(fromOptions)
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
    }

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

    protected SettingOption Option(
        string optionKey,
        string regPath,
        string regName,
        object value
    ) =>
        new(
            Loc.Instance[$"Customize.{OwnerKey}.{FeatureKey}.Options.{optionKey}"],
            value,
            [new RegistryBinding(regPath, regName, value)]
        );

    protected SettingOption Option(
        string optionKey,
        object value,
        params RegistryBinding[] bindings
    ) =>
        new(
            Loc.Instance[$"Customize.{OwnerKey}.{FeatureKey}.Options.{optionKey}"],
            value,
            bindings
        );

    protected string RecommendationPrefix => $"Customize.{OwnerKey}.{FeatureKey}.Recommendation";

    public virtual CustomizeRecommendationResult? GetRecommendation()
    {
        var state = Meta.Recommendation;
        if (state == RecommendationState.None)
            return null;

        return new CustomizeRecommendationResult(state, $"{RecommendationPrefix}.Reason");
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (a == null && b == null)
            return true;

        if (a == null || b == null)
            return false;

        if (a is IConvertible && b is IConvertible)
        {
            try
            {
                if (a.GetType() == b.GetType())
                    return a.Equals(b);

                var typeA = a.GetType();
                var typeB = b.GetType();

                if (
                    (
                        typeA == typeof(int)
                        || typeA == typeof(long)
                        || typeA == typeof(short)
                        || typeA == typeof(byte)
                    )
                    && (
                        typeB == typeof(int)
                        || typeB == typeof(long)
                        || typeB == typeof(short)
                        || typeB == typeof(byte)
                    )
                )
                {
                    return Convert.ToInt64(a) == Convert.ToInt64(b);
                }

                if (
                    (typeA == typeof(float) || typeA == typeof(double) || typeA == typeof(decimal))
                    && (
                        typeB == typeof(float)
                        || typeB == typeof(double)
                        || typeB == typeof(decimal)
                    )
                )
                {
                    return Convert.ToDouble(a) == Convert.ToDouble(b);
                }

                var da = Convert.ToDecimal(a);
                var db = Convert.ToDecimal(b);
                return da == db;
            }
            catch
            {
                // fall through to string comparison
            }
        }

        var strA = a.ToString();
        var strB = b.ToString();
        return strA != null && strB != null && strA.Equals(strB, StringComparison.Ordinal);
    }

    private static object? ReadPrimaryRawValue(IReadOnlyList<SettingOption> options)
    {
        // Read from the primary binding of the first option that has bindings
        foreach (var option in options)
        {
            if (option.PrimaryBinding is not { } binding)
                continue;

            return RegistryService.Read<object>(new RegistryItem(binding.Path, binding.Name));
        }

        return null;
    }
}
