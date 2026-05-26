using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Customize.Models;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Services.Managers;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Customize;

/// <summary>
/// Wraps an <see cref="ICustomizeSetting"/> for the UI.
/// Both toggle flips and value changes auto-apply immediately.
/// </summary>
public partial class CustomizeItemViewModel(ICustomizeSetting setting, ILoggerFactory loggerFactory)
    : ObservableObject
{
    private readonly ILogger<CustomizeItemViewModel> _logger =
        loggerFactory.CreateLogger<CustomizeItemViewModel>();

    private bool _hasLoaded;
    private CancellationTokenSource? _debounceCts;

    #region Public Pass-through Properties

    public ICustomizeSetting Setting => setting;
    public CustomizeControlType ControlType => setting.ControlType;
    public IReadOnlyList<SettingOption>? Options => setting.Options;
    public SymbolRegular Icon => setting.Icon;

    #endregion

    #region Observable Properties

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

    /// <summary>
    /// Bound by every control (ToggleSwitch, ComboBox, ListBox, NumberBox, TextBox).
    /// When the user interacts with the UI this value changes and auto-triggers apply.
    /// </summary>
    [ObservableProperty]
    private object? _currentValue;

    #endregion

    #region Recommendation UI

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

    #endregion

    #region State Management

    public async Task LoadStateAsync()
    {
        try
        {
            IsEnabled = await setting.GetStateAsync();
            CurrentValue = setting.CurrentValue;
            _hasLoaded = true;
        }
        catch
        {
            IsEnabled = false;
        }
    }

    /// <summary>
    /// Called by the ToggleSwitch. Always applies the opposite of the current state.
    /// </summary>
    [RelayCommand]
    private async Task ToggleAsync()
    {
        if (IsLoading)
            return;

        var newState = !IsEnabled;

        _logger.LogInformation(
            "Apply {Action} {Setting} ({Key})",
            newState ? "On" : "Off",
            setting.Name,
            setting.FeatureKey
        );

        IsLoading = true;
        try
        {
            using (ExecutionScope.BeginForLogging(_logger))
            {
                await setting.ApplyAsync(newState);
            }
            IsEnabled = await setting.GetStateAsync();
            CurrentValue = setting.CurrentValue;
            ((App)Application.Current).HasPendingChanges = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle {SettingName}", setting.Name);
            throw;
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnCurrentValueChanged(object? value)
    {
        if (!_hasLoaded || ControlType == CustomizeControlType.Toggle)
            return;

        // Debounce for text input; immediate for discrete selections
        if (ControlType == CustomizeControlType.String)
            _ = ApplyWithDebounceAsync(value);
        else
            _ = ApplyValueAsync(value);
    }

    private async Task ApplyValueAsync(object? value)
    {
        if (IsLoading)
            return;

        IsLoading = true;
        try
        {
            _logger.LogInformation(
            "===== START applying setting {Setting} ({Key}) =====",
            setting.Name,
            setting.FeatureKey
        );
            using (ExecutionScope.BeginForLogging(_logger))
            {
                await setting.ApplyAsync(value);
            }
            IsEnabled = await setting.GetStateAsync();
            ((App)Application.Current).HasPendingChanges = true;

            _logger.LogInformation(
                "Applied {Setting} ({Key}) = {Value}",
                setting.Name,
                setting.FeatureKey,
                value
            );

            _logger.LogInformation(
            "===== END applying setting {Setting} ({Key}) =====",
            setting.Name,
            setting.FeatureKey
        );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply {SettingName}", setting.Name);
            throw;
        }
        finally
        {
            IsLoading = false;
        }
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

            await ApplyValueAsync(value);
        }
        catch (TaskCanceledException)
        {
            // Expected on debounce reset
        }
    }

    #endregion
}
