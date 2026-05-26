using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class CustomizeViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly CustomizeRegistry _registry;

    [ObservableProperty]
    private ObservableCollection<CustomizeCategoryItemViewModel> _categories = [];

    [ObservableProperty]
    private bool _isLoading = true;

    public CustomizeViewModel(INavigationService navigationService, CustomizeRegistry registry)
    {
        _navigationService = navigationService;
        _registry = registry;
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;

        if (_registry.Categories.Length == 0)
            _registry.RegisterCategories();

        var categoryViewModels = new ObservableCollection<CustomizeCategoryItemViewModel>();

        foreach (var category in _registry.Categories)
            categoryViewModels.Add(
                new CustomizeCategoryItemViewModel
                {
                    Name = category.Name,
                    Description = category.Description,
                    Icon = category.Icon,
                    CategoryType = category.GetType(),
                    PageType = category
                        .GetType()
                        .GetCustomAttribute<CustomizeCategoryAttribute>()
                        ?.PageType,
                }
            );

        Categories = categoryViewModels;
        IsLoading = false;

        await Task.CompletedTask;
    }

    [RelayCommand]
    private void NavigateToCategory(CustomizeCategoryItemViewModel customizeCategoryItem)
    {
        if (customizeCategoryItem.PageType != null)
            _navigationService.Navigate(customizeCategoryItem.PageType);
    }
}

public partial class CustomizeCategoryItemViewModel : ObservableObject
{
    [ObservableProperty]
    private Type? _categoryType;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private SymbolRegular _icon;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private Type? _pageType;
}
