using System.Windows.Controls;
using StartupTask = optimizerDuck.Domain.Optimizations.Models.StartupManager.StartupTask;

namespace optimizerDuck.UI.Dialogs;

public partial class StartupTaskDetailsPanel : UserControl
{
    public StartupTaskDetailsPanel(StartupTask task)
    {
        InitializeComponent();
        DataContext = task;
    }
}
