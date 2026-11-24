using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace optimizerDuck.UI.Pages;

public partial class LegacyRevertsPage : INavigableView<LegacyRevertsViewModel>
{
    public LegacyRevertsPage(LegacyRevertsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }

    public LegacyRevertsViewModel ViewModel { get; }
}
