using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
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
public abstract partial class BaseCustomizeSetting : ObservableObject, ICustomizeSetting
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

    /// <summary>
    ///     Gets the compatibility condition type declared in the
    ///     <see cref="CustomizeSettingAttribute"/> (implementing <see cref="ICondition"/>),
    ///     or <c>null</c> when the setting is always available.
    /// </summary>
    public Type? ConditionType => Meta.Condition;

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

    /// <summary>
    ///     For Dropdown settings: the matched option's value, the raw registry value when
    ///     out of scope, or <see cref="MissingValueSentinel"/> when the value is missing.
    ///     Never interpret <c>null</c> as "unset" for Dropdown settings; use
    ///     <see cref="MissingValueSentinel"/> instead.
    /// </summary>
    public virtual object? CurrentValue
    {
        get
        {
            var options = GetOptions();
            if (ControlType != CustomizeControlType.Dropdown || options == null)
                return null;

            var resolution = ResolveDropdownValue(options);
            return resolution.Matched ? resolution.Value : resolution.Value ?? MissingValueSentinel;
        }
    }

    /// <summary>
    ///     Gets the options displayed for a Dropdown setting: the declared options plus,
    ///     when the current registry value falls outside them, a memory-only "Custom"/"Not
    ///     set" option reflecting the actual value. It is derived from the live registry
    ///     state and never persisted; it disappears once the value returns to a declared
    ///     option.
    /// </summary>
    public virtual IReadOnlyList<SettingOption>? Options
    {
        get
        {
            var options = GetOptions();
            if (ControlType != CustomizeControlType.Dropdown || options == null)
                return options;

            var resolution = ResolveDropdownValue(options);
            if (resolution.Matched)
                return options;

            // A partial multi-binding match can surface a raw value that already equals a
            // declared option's Value. Appending "Custom" then would duplicate that Value,
            // which makes SelectedValuePath="Value" selection ambiguous, so skip it.
            var displayValue = resolution.Value ?? MissingValueSentinel;
            if (options.Any(o => ValuesEqual(o.Value, displayValue)))
                return options;

            return [.. options, CreateCustomOption(displayValue)];
        }
    }

    /// <summary>
    ///     Stable, non-null sentinel used as the fallback option's value when the registry
    ///     value is missing entirely, so WPF can render a selection (null values cannot be
    ///     matched by <c>SelectedValuePath</c>).
    /// </summary>
    internal static readonly object MissingValueSentinel = new();

    /// <summary>
    ///     Creates the fallback option shown when the current registry value is outside
    ///     the declared options. When the value is missing entirely
    ///     (<paramref name="rawValue"/> is <see cref="MissingValueSentinel"/>), a distinct
    ///     "Not set" label is used; otherwise the generic "Custom" label shows the actual
    ///     out-of-scope value.
    /// </summary>
    private static SettingOption CreateCustomOption(object rawValue) =>
        new(
            ReferenceEquals(rawValue, MissingValueSentinel)
                ? Loc.Instance[CustomOptionNotSetTranslationKey]
                : Loc.Instance[CustomOptionTranslationKey],
            rawValue
        );

    /// <summary>The translation key used for the synthetic "Custom" option label.</summary>
    public const string CustomOptionTranslationKey = "Customize.CustomOption";

    /// <summary>
    ///     The translation key used for the synthetic fallback label when the registry
    ///     value is missing entirely (see <see cref="MissingValueSentinel"/>).
    /// </summary>
    public const string CustomOptionNotSetTranslationKey = "Customize.CustomOption.NotSet";

    protected virtual IReadOnlyList<SettingOption>? GetOptions() => null;

    /// <summary>
    ///     Reads the current registry state. For toggles: <c>true</c> when all required
    ///     toggles are on, <c>false</c> otherwise.
    /// </summary>
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

    /// <summary>
    ///     Reads state until two consecutive reads agree or retries run out. Use after
    ///     <see cref="ApplyAsync"/> to let the registry settle.
    /// </summary>
    /// <param name="maxRetries">The number of read attempts.</param>
    /// <param name="delayMs">The delay between attempts.</param>
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

    /// <summary>
    ///     Applies the value to the registry: for toggles a <see cref="bool"/>, for
    ///     dropdowns the matching declared option's bindings. Then runs any post-apply
    ///     Windows refresh declared by <see cref="RefreshScope"/>.
    /// </summary>
    /// <param name="value">The value to apply. The synthetic "Custom"/"Not set" fallback
    ///     value is a safe no-op.</param>
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
        else if (ControlType == CustomizeControlType.Dropdown && GetOptions() is { } options)
        {
            // Find the matching declared option and apply all its bindings. The
            // "Custom"/"Not set" fallback is never a declared option, so applying it
            // is a safe no-op.
            var option = options.FirstOrDefault(o => Equals(o.Value, value));
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
        string regPath,
        string regName,
        object value,
        bool matchMissingAsDefault
    ) =>
        new(
            Loc.Instance[$"Customize.{OwnerKey}.{FeatureKey}.Options.{optionKey}"],
            value,
            [
                new RegistryBinding(
                    regPath,
                    regName,
                    value,
                    Microsoft.Win32.RegistryValueKind.DWord,
                    matchMissingAsDefault ? [value, null] : null
                ),
            ]
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

    protected RegistryBinding Bind(
        string regPath,
        string? regName,
        object? value,
        params object?[] additionalMatchValues
    ) =>
        new(
            regPath,
            regName,
            value,
            Microsoft.Win32.RegistryValueKind.DWord,
            additionalMatchValues.Length > 0 ? [value, .. additionalMatchValues] : null
        );

    protected RegistryBinding Bind(
        string regPath,
        string? regName,
        object? value,
        Microsoft.Win32.RegistryValueKind valueKind,
        params object?[] additionalMatchValues
    ) =>
        new(
            regPath,
            regName,
            value,
            valueKind,
            additionalMatchValues.Length > 0 ? [value, .. additionalMatchValues] : null
        );

    protected RegistryBinding BindWithDefault(
        string regPath,
        string? regName,
        object value
    ) =>
        new(
            regPath,
            regName,
            value,
            Microsoft.Win32.RegistryValueKind.DWord,
            [value, null]
        );

    protected string RecommendationPrefix => $"Customize.{OwnerKey}.{FeatureKey}.Recommendation";

    /// <summary>
    ///     Returns the recommendation declared in the <see cref="CustomizeSettingAttribute"/>,
    ///     or <c>null</c> when the setting has none.
    /// </summary>
    public virtual CustomizeRecommendationResult? GetRecommendation()
    {
        var state = Meta.Recommendation;
        if (state == RecommendationState.None)
            return null;

        return new CustomizeRecommendationResult(state, $"{RecommendationPrefix}.Reason");
    }

    public static bool ValuesEqual(object? a, object? b)
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

    /// <summary>
    ///     Matches the live registry values against the declared options. When every
    ///     binding of an option matches, the option is returned; otherwise the raw value
    ///     of the primary binding is returned so out-of-scope values stay visible.
    /// </summary>
    private static (bool Matched, object? Value) ResolveDropdownValue(
        IReadOnlyList<SettingOption> options
    )
    {
        foreach (var option in options)
        {
            if (option.Bindings is not { Count: > 0 })
                continue;

            var allMatch = option.Bindings.All(b =>
            {
                var actual = RegistryService.Read<object>(new RegistryItem(b.Path, b.Name));
                return b.Matches(actual);
            });

            if (allMatch)
                return (true, option.Value);
        }

        return (false, ReadPrimaryRawValue(options));
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
