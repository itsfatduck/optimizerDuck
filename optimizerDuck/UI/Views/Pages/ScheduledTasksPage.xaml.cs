using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace optimizerDuck.UI.Views.Pages;

public partial class ScheduledTasksPage : INavigableView<ScheduledTasksViewModel>
{
    public ScheduledTasksPage(ScheduledTasksViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }

    public ScheduledTasksViewModel ViewModel { get; }
}
