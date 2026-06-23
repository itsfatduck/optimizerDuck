using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Customize.Models;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Customize;

public partial class CustomizeItemViewModel(
    ICustomizeSetting setting,
    ILoggerFactory loggerFactory,
    IRegistryWatcher registryWatcher
) : ObservableObject, IDisposable
{
    private readonly ILogger<CustomizeItemViewModel> _logger =
        loggerFactory.CreateLogger<CustomizeItemViewModel>();

    private bool _hasLoaded;
    private CancellationTokenSource? _debounceCts;
    private bool _disposed;

    private readonly HashSet<string> _watchedPaths = new(StringComparer.OrdinalIgnoreCase);

    private readonly object _applyLock = new();
    private bool _isApplying;
    private object? _pendingValue;
    private bool _hasPendingValue;

    public ICustomizeSetting Setting => setting;
    public CustomizeControlType ControlType => setting.ControlType;
    public IReadOnlyList<SettingOption>? Options => setting.Options;
    public SymbolRegular Icon => setting.Icon;

    [ObservableProperty]
    private string _description = setting.Description;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _name = setting.Name;

    [ObservableProperty]
    private string _section = setting.Section;

    [ObservableProperty]
    private string _categoryName = string.Empty;

    [ObservableProperty]
    private object? _currentValue;

    public CustomizeRecommendationResult? Recommendation => setting.GetRecommendation();
    public bool HasRecommendation => Recommendation != null;

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

    public async Task LoadStateAsync()
    {
        try
        {
            IsEnabled = await setting.GetStateAsync();
            CurrentValue = setting.CurrentValue;
            _hasLoaded = true;

            SubscribeToRegistryChanges();
        }
        catch
        {
            IsEnabled = false;
        }
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

    private async void OnRegistryKeyChanged(object? sender, string path)
    {
        if (_disposed || !_hasLoaded)
            return;

        if (!_watchedPaths.Contains(path))
            return;

        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                lock (_applyLock)
                {
                    if (_isApplying)
                        return;
                }

                var state = await setting.GetStateWithRetryAsync(maxRetries: 4, delayMs: 80);
                IsEnabled = state;
                CurrentValue = setting.CurrentValue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "RegistryWatcher: failed to refresh state for {Setting}",
                    setting.Name
                );
            }
        });
    }

    [RelayCommand]
    private void Toggle()
    {
        lock (_applyLock)
        {
            var currentTarget = _hasPendingValue ? (bool)_pendingValue! : IsEnabled;
            var nextState = !currentTarget;

            _pendingValue = nextState;
            _hasPendingValue = true;

            if (_isApplying)
                return;

            _isApplying = true;
        }

        _ = ProcessPendingValuesAsync();
    }

    partial void OnCurrentValueChanged(object? value)
    {
        if (!_hasLoaded || ControlType == CustomizeControlType.Toggle)
            return;

        if (Equals(value, setting.CurrentValue))
            return;

        if (ControlType == CustomizeControlType.String)
            _ = ApplyWithDebounceAsync(value);
        else
            QueueApplyValue(value);
    }

    private void QueueApplyValue(object? value)
    {
        lock (_applyLock)
        {
            _pendingValue = value;
            _hasPendingValue = true;

            if (_isApplying)
                return;

            _isApplying = true;
        }

        _ = ProcessPendingValuesAsync();
    }

    private async Task ApplyWithDebounceAsync(object? value)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        try
        {
            await Task.Delay(400, token);
            if (token.IsCancellationRequested)
                return;

            QueueApplyValue(value);
        }
        catch (TaskCanceledException) { }
    }

    private async Task ProcessPendingValuesAsync()
    {
        try
        {
            while (true)
            {
                object? valueToApply;
                lock (_applyLock)
                {
                    if (!_hasPendingValue)
                    {
                        _isApplying = false;
                        break;
                    }

                    valueToApply = _pendingValue;
                    _hasPendingValue = false;
                }

                IsLoading = true;
                try
                {
                    _logger.LogInformation(
                        "Apply {Value} for {Setting} ({Key})",
                        valueToApply,
                        setting.Name,
                        setting.FeatureKey
                    );

                    using (ExecutionScope.BeginForLogging(_logger))
                    {
                        await setting.ApplyAsync(valueToApply);
                    }

                    IsEnabled = await setting.GetStateWithRetryAsync();

                    if (ControlType != CustomizeControlType.Toggle)
                    {
                        CurrentValue = setting.CurrentValue;
                    }

                    ((App)Application.Current).HasPendingChanges = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to apply {SettingName}", setting.Name);
                }
            }
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
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
    }
}
