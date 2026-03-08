using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class ToggleFeatureCategoryViewModel : ViewModel
{
    [ObservableProperty] private string _categoryName = string.Empty;
    [ObservableProperty] private string _categoryDescription = string.Empty;
    [ObservableProperty] private SymbolRegular _categoryIcon;
    [ObservableProperty] private ObservableCollection<FeatureViewModel> _features = [];
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _selectedSortByIndex;
    [ObservableProperty] private bool _isLoading = true;

    private IToggleFeatureCategory? _currentCategory;
    private List<FeatureViewModel> _allFeatures = [];

    public ToggleFeatureCategoryViewModel(IToggleFeatureCategory category)
    {
        _currentCategory = category;

        CategoryName = _currentCategory.Name;
        CategoryDescription = _currentCategory.Description;
        CategoryIcon = _currentCategory.Icon;

        _allFeatures.Clear();
        foreach (var feature in _currentCategory.Features)
        {
            _allFeatures.Add(new FeatureViewModel(feature));
        }

        foreach (var feature in _allFeatures)
        {
            _ = feature.LoadStateAsync();
        }

        ApplyFilters();
        IsLoading = false;
    }

    public override async Task OnNavigatedToAsync()
    {
        await base.OnNavigatedToAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedSortByIndexChanged(int value)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        if (_currentCategory == null) return;

        var filtered = _allFeatures.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.ToLowerInvariant();
            filtered = filtered.Where(f => 
                f.Name.ToLowerInvariant().Contains(search) || 
                f.Description.ToLowerInvariant().Contains(search));
        }

        // Apply sorting
        // 0 = Name (A-Z)
        // 1 = Name (Z-A)
        // We can match what DiskCleanup or other pages do, but simple for now
        if (SelectedSortByIndex == 0) // Default: Name
        {
            filtered = filtered.OrderBy(f => f.Name);
        }

        Features = new ObservableCollection<FeatureViewModel>(filtered);
        
        foreach (var feature in Features)
        {
            feature.IsVisible = true;
        }
    }
}
