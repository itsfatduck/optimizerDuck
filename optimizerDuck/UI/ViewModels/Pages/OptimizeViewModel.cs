using System.Collections.ObjectModel;
using System.Reflection;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Services;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Pages;

public class OptimizeViewModel(OptimizationRegistry optimizationService) : ViewModel
{
    private bool _isInitialized;

    public ObservableCollection<NavigationViewItem> OptimizationCategories { get; } = [];

    public event Action? OptimizationsLoaded;

    public override Task OnNavigatedToAsync()
    {
        // Load optimization categories when user navigates to the page
        if (_isInitialized)
            return Task.CompletedTask;

        foreach (var category in optimizationService.OptimizationCategories)
            OptimizationCategories.Add(new NavigationViewItem
            {
                Content = category.Name,
                TargetPageType = category.GetType().GetCustomAttribute<OptimizationCategoryAttribute>()!.PageType
            });

        OptimizationsLoaded?.Invoke();
        _isInitialized = true;
        return Task.CompletedTask;
    }
}