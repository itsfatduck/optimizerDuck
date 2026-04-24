using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Execution;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Features;

public partial class FeatureViewModel(IFeature feature, ILoggerFactory loggerFactory)
    : ObservableObject
{
    private readonly ILogger<FeatureViewModel> _logger =
        loggerFactory.CreateLogger<FeatureViewModel>();

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
