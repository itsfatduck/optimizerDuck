using System.Collections.ObjectModel;
using System.Reflection;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Services;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Pages;

public class OptimizeViewModel(OptimizationRegistry optimizationService) : ViewModel
{

    public ObservableCollection<NavigationViewItem> OptimizationCategories { get; } = [];

    public event Action? OptimizationsLoaded;

    protected override async Task InitializeOnceAsync()
    {
        await optimizationService.EnsurePreloadedAsync().ConfigureAwait(true);

        foreach (var category in optimizationService.OptimizationCategories)
            OptimizationCategories.Add(
                new NavigationViewItem
                {
                    Content = category.Name,
                    TargetPageType = category
                        .GetType()
                        .GetCustomAttribute<OptimizationCategoryAttribute>()!
                        .PageType,
                }
            );

        OptimizationsLoaded?.Invoke();
    }
}
