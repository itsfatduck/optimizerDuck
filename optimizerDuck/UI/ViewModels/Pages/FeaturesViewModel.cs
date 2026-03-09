using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Services;
using optimizerDuck.UI.Views.Pages.Features;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class FeaturesViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<FeatureCategoryItemViewModel> _categories = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading = true;

    private readonly INavigationService _navigationService;
    private readonly FeatureRegistry _registry;

    public FeaturesViewModel(INavigationService navigationService, FeatureRegistry registry)
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

        var categoryViewModels = new ObservableCollection<FeatureCategoryItemViewModel>();

        foreach (var category in _registry.Categories)
        {
            categoryViewModels.Add(new FeatureCategoryItemViewModel
            {
                Name = category.Name,
                Description = category.Description,
                Icon = category.Icon,
                CategoryType = category.GetType(),
                PageType = category.GetType().GetCustomAttribute<FeatureCategoryAttribute>()?.PageType
            });
        }

        Categories = categoryViewModels;
        IsLoading = false;

        await Task.CompletedTask;
    }

    [RelayCommand]
    private void NavigateToCategory(FeatureCategoryItemViewModel featureCategoryItem)
    {
        if (featureCategoryItem.PageType != null)
        {
            _navigationService.Navigate(featureCategoryItem.PageType);
        }
    }
}

public partial class FeatureCategoryItemViewModel : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private SymbolRegular _icon;
    [ObservableProperty] private Type? _categoryType;
    [ObservableProperty] private Type? _pageType;
}