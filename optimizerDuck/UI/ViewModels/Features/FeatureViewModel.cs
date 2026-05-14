using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Features.Models;
using optimizerDuck.Services.Managers;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Features;

public partial class FeatureViewModel(IFeature feature, ILoggerFactory loggerFactory)
    : ObservableObject
{
    private readonly ILogger<FeatureViewModel> _logger =
        loggerFactory.CreateLogger<FeatureViewModel>();

    public IFeature Feature => feature;

    [ObservableProperty]
    private string _description = feature.Description;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _name = feature.Name;

    [ObservableProperty]
    private string _section = feature.Section;

    public SymbolRegular Icon => feature.Icon;

    public FeatureRecommendationResult? Recommendation => feature.GetRecommendation();

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
            IsEnabled = await feature.GetStateAsync();
        }
        catch
        {
            IsEnabled = false;
        }
    }

    [RelayCommand]
    private async Task ToggleAsync()
    {
        if (IsLoading)
            return;

        var isEnabling = !IsEnabled;

        _logger.LogInformation(
            "===== START {Action} toggle feature {FeatureName} ({FeatureKey}) =====",
            isEnabling ? "enabling" : "disabling",
            feature.Name,
            feature.FeatureKey
        );

        var scope = ExecutionScope.BeginForLogging(_logger);

        IsLoading = true;
        try
        {
            if (isEnabling)
            {
                await feature.EnableAsync();
                IsEnabled = true;
            }
            else
            {
                await feature.DisableAsync();
                IsEnabled = false;
            }

            ((App)Application.Current).HasPendingChanges = true;

            scope.Dispose();

            _logger.LogInformation(
                "===== END {Action} toggle feature {FeatureName} ({FeatureKey}) =====",
                isEnabling ? "enabling" : "disabling",
                feature.Name,
                feature.FeatureKey
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle feature {FeatureName}", feature.Name);
            throw;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
