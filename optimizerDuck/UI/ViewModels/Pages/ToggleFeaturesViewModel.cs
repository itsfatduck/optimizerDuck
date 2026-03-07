using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Services;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class ToggleFeaturesViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<CategoryViewModel> _categories = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading = true;

    public ToggleFeaturesViewModel()
    {
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;

        var registry = ToggleFeaturesRegistry.Instance;
        if (registry.Categories.Length == 0)
        {
            registry.RegisterCategories();
        }

        var categoryViewModels = new ObservableCollection<CategoryViewModel>();

            foreach (var category in registry.Categories)
            {
                var categoryVm = new CategoryViewModel
                {
                    Name = category.Name,
                    Features = new ObservableCollection<FeatureViewModel>()
                };

                foreach (var feature in category.Features)
                {
                    var featureVm = new FeatureViewModel(feature);
                    categoryVm.Features.Add(featureVm);
                }

                categoryViewModels.Add(categoryVm);
            }

            foreach (var category in categoryViewModels)
            {
                foreach (var feature in category.Features)
                {
                    _ = feature.LoadStateAsync();
                }
            }

        Categories = categoryViewModels;
        IsLoading = false;
    }

    partial void OnSearchTextChanged(string value)
    {
        foreach (var category in Categories)
        {
            category.FilterFeatures(value);
        }
    }
}

public partial class CategoryViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private ObservableCollection<FeatureViewModel> _features = [];

    [ObservableProperty]
    private bool _hasVisibleFeatures = true;

    public void FilterFeatures(string searchText)
    {
        var searchLower = searchText.ToLowerInvariant();
        
        foreach (var feature in Features)
        {
            feature.IsVisible = string.IsNullOrEmpty(searchText) ||
                feature.Name.ToLowerInvariant().Contains(searchLower) ||
                feature.Description.ToLowerInvariant().Contains(searchLower);
        }

        HasVisibleFeatures = Features.Any(f => f.IsVisible);
    }
}

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
