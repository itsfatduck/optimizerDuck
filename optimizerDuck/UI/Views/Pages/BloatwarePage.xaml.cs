using System.Windows.Controls;
using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace optimizerDuck.UI.Views.Pages;

public partial class BloatwarePage : INavigableView<BloatwareViewModel>
{
    public BloatwarePage(BloatwareViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }

    public BloatwareViewModel ViewModel { get; }
}