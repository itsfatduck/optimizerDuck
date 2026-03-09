using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Features;
using optimizerDuck.Resources.Languages;
using optimizerDuck.UI.ViewModels.Features;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class FeatureCategoryViewModel : ViewModel
{
    [ObservableProperty] private string _categoryName = string.Empty;
    [ObservableProperty] private string _categoryDescription = string.Empty;
    [ObservableProperty] private SymbolRegular _categoryIcon;
    [ObservableProperty] private ObservableCollection<FeatureSection> _sections = [];
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _selectedSortByIndex;
    [ObservableProperty] private bool _isLoading = true;

    private readonly IFeatureCategory? _currentCategory;
    private readonly List<FeatureViewModel> _allFeatures = [];

    public FeatureCategoryViewModel(IFeatureCategory category, ILoggerFactory loggerFactory)
    {
        _currentCategory = category;

        CategoryName = _currentCategory.Name;
        CategoryDescription = _currentCategory.Description;
        CategoryIcon = _currentCategory.Icon;

        _allFeatures.Clear();
        foreach (var feature in _currentCategory.Features)
        {
            _allFeatures.Add(new FeatureViewModel(feature, loggerFactory));
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
                f.Name.Contains(search, StringComparison.InvariantCultureIgnoreCase) || 
                f.Description.Contains(search, StringComparison.InvariantCultureIgnoreCase));
        }

        var sortedFeatures = filtered.OrderBy<FeatureViewModel, string>(f => f.Name).ToList();

        var grouped = sortedFeatures
            .GroupBy(f => string.IsNullOrEmpty(f.Section) ? Translations.Common_Other : f.Section)
            .OrderBy(g => g.Key == Translations.Common_Other ? 1 : 0)
            .ThenBy(g => g.Key);

        var sections = new ObservableCollection<FeatureSection>();
        foreach (var group in grouped)
        {
            var section = new FeatureSection
            {
                Name = group.Key,
                Features = new ObservableCollection<FeatureViewModel>(group.ToList())
            };
            sections.Add(section);

            foreach (var feature in section.Features)
            {
                feature.IsVisible = true;
            }
        }

        Sections = sections;
    }
}
