using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace optimizerDuck.UI.Views.Pages;

public partial class DiskCleanupPage : INavigableView<DiskCleanupViewModel>
{
    public DiskCleanupPage(DiskCleanupViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }

    public DiskCleanupViewModel ViewModel { get; }
}
