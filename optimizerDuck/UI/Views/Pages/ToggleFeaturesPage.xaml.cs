using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace optimizerDuck.UI.Views.Pages.ToggleFeatures;

public partial class ToggleFeaturesPage : INavigableView<ToggleFeaturesViewModel>
{
    public ToggleFeaturesViewModel ViewModel { get; }

    public ToggleFeaturesPage(ToggleFeaturesViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();

        Loaded += async (_, _) => await ViewModel.InitializeAsync();
    }
}
