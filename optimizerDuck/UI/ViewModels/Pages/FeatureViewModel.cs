using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using optimizerDuck.Core.Interfaces;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class FeatureViewModel(IToggleFeature feature) : ObservableObject
{
    [ObservableProperty]
    private string _name = feature.Name;

    [ObservableProperty]
    private string _description = feature.Description;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _isLoading;

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

        IsLoading = true;
        try
        {
            if (IsEnabled)
            {
                await feature.DisableAsync();
                IsEnabled = false;
            }
            else
            {
                await feature.EnableAsync();
                IsEnabled = true;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
