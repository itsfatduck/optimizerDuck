using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Conditions;
using optimizerDuck.Domain.Customize.Models;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.Customize;
using optimizerDuck.Services.System;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Customize;

public partial class CustomizeItemViewModel(
    ICustomizeSetting setting,
    ILoggerFactory loggerFactory,
    IRegistryWatcher registryWatcher
) : LocalizedObject, IDisposable
{
    private readonly ILogger<CustomizeItemViewModel> _logger =
        loggerFactory.CreateLogger<CustomizeItemViewModel>();

    private bool _hasLoaded;
    private readonly CustomizationExecutor _executor = new(
        loggerFactory.CreateLogger<CustomizationExecutor>()
    );
    private bool _disposed;

    private readonly HashSet<string> _watchedPaths = new(StringComparer.OrdinalIgnoreCase);

    public ICustomizeSetting Setting => setting;
    public CustomizeControlType ControlType => setting.ControlType;

    // Populated by LoadStateAsync: materializing Options here would perform registry
    // I/O on the UI thread during construction.
    [ObservableProperty]
    private IReadOnlyList<SettingOption>? _options;

    public SymbolRegular Icon => setting.Icon;

    /// <summary>
    ///     Computed so grouped section headers, filters and bindings always re-resolve
    ///     against the current culture, even when a parent handler rebuilds sections
    ///     before this item is notified.
    /// </summary>
    public string Description => setting.Description;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleCommand))]
    private bool _isLoading;

    public string Name => setting.Name;

    public string Section => setting.Section;

    [ObservableProperty]
    private object? _currentValue;

    /// <summary>Gets or sets the evaluated compatibility result for this setting.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUnsupported))]
    [NotifyPropertyChangedFor(nameof(ConditionTitle))]
    [NotifyPropertyChangedFor(nameof(ConditionDescription))]
    private ConditionResult _conditionResult = ConditionResult.Available;

    public CustomizeRecommendationResult? Recommendation => setting.GetRecommendation();
    public bool HasRecommendation => Recommendation != null;

    /// <summary>Gets whether this setting is unsupported on the current system.</summary>
    public bool IsUnsupported => ConditionResult.IsBlocking;

    /// <summary>Gets the localized condition failure title, or <c>null</c>.</summary>
    public string? ConditionTitle => ConditionResult.Title;

    /// <summary>Gets the localized condition failure description, or <c>null</c>.</summary>
    public string? ConditionDescription => ConditionResult.Description;

    public string? RecommendationStateDisplay =>
        Recommendation?.State switch
        {
            RecommendationState.On => Loc.Instance["Common.Recommendation.On"],
            RecommendationState.Off => Loc.Instance["Common.Recommendation.Off"],
            RecommendationState.Experimental => Loc.Instance["Common.Recommendation.Experimental"],
            RecommendationState.Depends => Loc.Instance["Common.Recommendation.Depends"],
            _ => null,
        };

    public SymbolRegular RecommendationIcon =>
        Recommendation?.State switch
        {
            RecommendationState.On => SymbolRegular.Checkmark24,
            RecommendationState.Off => SymbolRegular.Dismiss24,
            RecommendationState.Experimental => SymbolRegular.Beaker24,
            RecommendationState.Depends => SymbolRegular.PersonQuestionMark24,
            _ => SymbolRegular.PersonQuestionMark24,
        };

    public string? RecommendationReason =>
        Recommendation != null ? Loc.Instance[Recommendation.ReasonTranslationKey] : null;

    /// <summary>
    ///     Loads the setting's current state, effective options and value, then subscribes
    ///     to registry changes. Registry I/O runs on the thread pool.
    /// </summary>
    public async Task LoadStateAsync()
    {
        try
        {
            // Registry I/O (state query, options, current value) runs on the thread pool
            // so page load never blocks the UI thread.
            IsEnabled = await Task.Run(() => setting.GetStateAsync());

            // Publish the effective options before the selection value so the ComboBox
            // item list already contains the value when the SelectedValue binding resolves.
            var options = await Task.Run(() => setting.Options);
            UpdateOptions(options);

            CurrentValue = await Task.Run(() => setting.CurrentValue);
            _hasLoaded = true;

            SubscribeToRegistryChanges();
        }
        catch (Exception ex)
        {
            // keep state unloaded on read failure instead of showing it as off.
            _logger.LogWarning(ex, "Failed to load customize setting {Setting}", setting.LogName());
        }
    }

    /// <summary>
    ///     Publishes the given options only when the list actually changed, so the ComboBox
    ///     isn't needlessly rebuilt.
    /// </summary>
    private void UpdateOptions(IReadOnlyList<SettingOption>? options)
    {
        if (HasSameOptionValues(Options, options))
            return;

        Options = options;
    }

    /// <summary>
    ///     Re-publishes dropdown options so their display text re-resolves in the
    ///     new language. Option values are unchanged; only <c>Display</c> refreshes.
    /// </summary>
    protected override void OnLanguageChanged(CultureInfo newCulture)
    {
        if (!_hasLoaded)
            return;
        try
        {
            Options = setting.Options;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh options after language change");
        }
    }

    private static bool HasSameOptionValues(
        IReadOnlyList<SettingOption>? a,
        IReadOnlyList<SettingOption>? b
    )
    {
        if (a is null || b is null)
            return a is null && b is null;

        if (a.Count != b.Count)
            return false;

        for (var i = 0; i < a.Count; i++)
            if (!Equals(a[i].Value, b[i].Value))
                return false;

        return true;
    }

    private void SubscribeToRegistryChanges()
    {
        if (_disposed)
            return;

        foreach (var path in setting.WatchedRegistryPaths)
        {
            if (_watchedPaths.Add(path))
                registryWatcher.Watch(path);
        }

        if (_watchedPaths.Count > 0)
            registryWatcher.RegistryKeyChanged += OnRegistryKeyChanged;
    }

    private void OnRegistryKeyChanged(object? sender, string path)
    {
        if (_disposed || !_hasLoaded)
            return;

        if (!_watchedPaths.Contains(path))
            return;

        // The watcher raises on a background thread, so run the refresh on the UI thread.
        // UiThread.InvokeAsync resolves to the Func<Task> overload (the method returns
        // Task), so the refresh task is observed rather than fire-and-forgotten. When no
        // WPF Application exists (unit tests), it runs inline instead.
        _ = UiThread.InvokeAsync(RefreshFromRegistryAsync);
    }

    /// <summary>
    ///     Re-reads the live registry state and republishes the effective options and the
    ///     selection value. Marshalled to the UI thread by <see cref="OnRegistryKeyChanged"/>
    ///     when a watched key changes; also called directly from tests.
    /// </summary>
    internal async Task RefreshFromRegistryAsync()
    {
        try
        {
            if (_executor.IsApplying)
                return;

            // Only the refresh bookkeeping runs on the UI thread; the reads stay off it.
            IsEnabled = await Task.Run(() =>
                setting.GetStateWithRetryAsync(maxRetries: 4, delayMs: 80)
            );
            var options = await Task.Run(() => setting.Options);
            UpdateOptions(options);
            CurrentValue = await Task.Run(() => setting.CurrentValue);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "RegistryWatcher: failed to refresh state for {Setting}",
                setting.LogName()
            );
        }
    }

    [RelayCommand(CanExecute = nameof(CanToggle))]
    private void Toggle()
    {
        // the first call marks the executor busy synchronously, so a same-tick second click is ignored.
        if (_executor.IsApplying)
            return;
        _ = _executor.ApplyWithDebounceAsync(!IsEnabled, ApplyCoreAsync, debounceMs: 0);
    }

    private bool CanToggle() => !IsLoading;

    partial void OnCurrentValueChanged(object? value)
    {
        if (!_hasLoaded || ControlType == CustomizeControlType.Toggle)
            return;

        if (Equals(value, setting.CurrentValue))
            return;

        var debounce = ControlType == CustomizeControlType.String ? 400 : 0;
        _ = _executor.ApplyWithDebounceAsync(value, ApplyCoreAsync, debounce);
    }

    private async Task ApplyCoreAsync(object? valueToApply)
    {
        IsLoading = true;
        try
        {
            _logger.LogInformation(
                "Apply {Value} for {Setting} ({Key})",
                valueToApply,
                setting.LogName(),
                setting.FeatureKey
            );

            var applyResult = await setting.ApplyAsync(
                valueToApply,
                new OpCall { Logger = _logger }
            );

            if (!applyResult.Ok)
            {
                _logger.LogError(
                    "Failed to apply {Setting} ({Key}): {Error} {Detail}",
                    setting.LogName(),
                    setting.FeatureKey,
                    applyResult.Error,
                    applyResult.ErrorDetail
                );
            }

            IsEnabled = await Task.Run(() => setting.GetStateWithRetryAsync());

            if (ControlType != CustomizeControlType.Toggle)
            {
                var (options, current) = await Task.Run(() =>
                    (setting.Options, setting.CurrentValue)
                );
                UpdateOptions(options);
                CurrentValue = current;
            }

            // only flag reboot when the value was actually written.
            if (applyResult.Ok && Application.Current is App app)
                app.HasPendingChanges = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply {SettingName}", setting.LogName());
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        registryWatcher.RegistryKeyChanged -= OnRegistryKeyChanged;

        foreach (var path in _watchedPaths)
            registryWatcher.Unwatch(path);

        _watchedPaths.Clear();
        _executor.Dispose();
    }
}
