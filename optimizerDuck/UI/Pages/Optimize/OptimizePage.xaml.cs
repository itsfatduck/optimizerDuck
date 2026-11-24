using System.Windows.Threading;
using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Abstractions.Controls;

namespace optimizerDuck.UI.Pages;

public partial class OptimizePage : INavigableView<OptimizeViewModel>
{
    public OptimizePage(OptimizeViewModel viewModel, INavigationViewPageProvider pageProvider)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();

        RootNavigation.SetPageProviderService(pageProvider);
        ViewModel.OptimizationsLoaded += OnOptimizationsLoaded;
        ViewModel.LegacyTabRemoved += OnLegacyTabRemoved;
    }

    public OptimizeViewModel ViewModel { get; }

    private void OnOptimizationsLoaded() =>
        Dispatcher.BeginInvoke(NavigateToFirstCategory, DispatcherPriority.ContextIdle);

    private void OnLegacyTabRemoved()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (
                RootNavigation.SelectedItem is null
                || RootNavigation.SelectedItem.TargetPageType == typeof(LegacyRevertsPage)
            )
            {
                NavigateToFirstCategory();
            }
        });
    }

    private void NavigateToFirstCategory()
    {
        var first = ViewModel.OptimizationCategories.FirstOrDefault();
        if (first?.TargetPageType != null)
            RootNavigation.Navigate(first.TargetPageType);
    }
}
