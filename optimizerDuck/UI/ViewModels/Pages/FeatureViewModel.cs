using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using optimizerDuck.Core.Interfaces;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class FeatureViewModel : ObservableObject
{
    private readonly IToggleFeature _feature;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _isLoading;

    public FeatureViewModel(IToggleFeature feature)
    {
        _feature = feature;
        _name = feature.Name;
        _description = feature.Description;
    }

    public async Task LoadStateAsync()
    {
        try
        {
            IsEnabled = await _feature.GetStateAsync();
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

        IsLoading = true;
        try
        {
            if (IsEnabled)
            {
                await _feature.DisableAsync();
                IsEnabled = false;
            }
            else
            {
                await _feature.EnableAsync();
                IsEnabled = true;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
