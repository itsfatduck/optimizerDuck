using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace optimizerDuck.UI.Pages.Customize;

public partial class CustomizeCategoryPage : INavigableView<CustomizeCategoryViewModel>
{
    public CustomizeCategoryPage(CustomizeCategoryViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }

    public CustomizeCategoryViewModel ViewModel { get; }
}
