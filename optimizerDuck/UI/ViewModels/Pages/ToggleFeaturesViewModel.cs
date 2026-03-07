using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using optimizerDuck.Core.ToggleFeatures;
using optimizerDuck.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class ToggleFeaturesViewModel : ViewModel
{
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private ObservableCollection<ToggleFeatureCategory> _categories = [];

    public ToggleFeaturesViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public void Initialize()
    {
        var registry = ToggleFeaturesRegistry.Instance;
        if (registry.Categories.Count == 0)
        {
            registry.RegisterCategories();
        }
        Categories = registry.Categories;
    }

    [RelayCommand]
    private void NavigateToCategory(ToggleFeatureCategory category)
    {
        if (category.PageType != null)
        {
            _navigationService.Navigate(category.PageType);
        }
    }
}
