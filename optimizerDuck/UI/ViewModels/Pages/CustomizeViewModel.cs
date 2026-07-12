using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Services.Customize;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class CustomizeViewModel : ViewModel
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


    protected override async Task InitializeOnceAsync()
    {
        IsLoading = true;

        await _registry.EnsurePreloadedAsync().ConfigureAwait(false);

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
