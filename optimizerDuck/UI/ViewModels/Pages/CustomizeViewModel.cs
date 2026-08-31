using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Abstractions;
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

        ReloadCategories();
        IsLoading = false;
    }

    private void ReloadCategories()
    {
        var categoryViewModels = new ObservableCollection<CustomizeCategoryItemViewModel>();
        foreach (var category in _registry.Categories)
        {
            categoryViewModels.Add(
                new CustomizeCategoryItemViewModel
                {
                    Source = category,
                    CategoryType = category.GetType(),
                    Icon = category.Icon,
                    PageType = category
                        .GetType()
                        .GetCustomAttribute<CustomizeCategoryAttribute>()
                        ?.PageType,
                }
            );
        }

        Categories = categoryViewModels;
    }

    [RelayCommand]
    private void NavigateToCategory(CustomizeCategoryItemViewModel customizeCategoryItem)
    {
        if (customizeCategoryItem.PageType != null)
            _navigationService.Navigate(customizeCategoryItem.PageType);
    }
}

public partial class CustomizeCategoryItemViewModel : LocalizedObject
{
    [ObservableProperty]
    private Type? _categoryType;

    /// <summary>
    ///     The category whose display strings this card forwards. Set at construction
    ///     time; <see cref="Name" /> and <see cref="Description" /> re-resolve on every
    ///     culture change.
    /// </summary>
    public ICustomizeCategory? Source { get; set; }

    public string Description => Source?.Description ?? string.Empty;
    public string Name => Source?.Name ?? string.Empty;

    [ObservableProperty]
    private SymbolRegular _icon;

    [ObservableProperty]
    private Type? _pageType;
}
