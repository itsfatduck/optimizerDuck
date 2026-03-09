using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Services;
using optimizerDuck.UI.Views.Pages.ToggleFeatures;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class ToggleFeaturesViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<ToggleFeaturesCategoryViewModel> _categories = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading = true;

    private readonly INavigationService _navigationService;
    private readonly ToggleFeaturesRegistry _registry;

    public ToggleFeaturesViewModel(INavigationService navigationService, ToggleFeaturesRegistry registry)
    {
        _navigationService = navigationService;
        _registry = registry;
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;

        if (_registry.Categories.Length == 0)
        {
            _registry.RegisterCategories();
        }

        var categoryViewModels = new ObservableCollection<ToggleFeaturesCategoryViewModel>();

        foreach (var category in _registry.Categories)
        {
            categoryViewModels.Add(new ToggleFeaturesCategoryViewModel
            {
                Name = category.Name,
                Description = category.Description,
                Icon = category.Icon,
                CategoryType = category.GetType(),
                PageType = category.GetType().GetCustomAttribute<ToggleFeatureCategoryAttribute>()?.PageType
            });
        }

        Categories = categoryViewModels;
        IsLoading = false;

        await Task.CompletedTask;
    }

    [RelayCommand]
    private void NavigateToCategory(ToggleFeaturesCategoryViewModel toggleFeaturesCategory)
    {
        if (toggleFeaturesCategory.PageType != null)
        {
            _navigationService.Navigate(toggleFeaturesCategory.PageType);
        }
    }
}

public partial class ToggleFeaturesCategoryViewModel : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private SymbolRegular _icon;
    [ObservableProperty] private Type? _categoryType;
    [ObservableProperty] private Type? _pageType;
}