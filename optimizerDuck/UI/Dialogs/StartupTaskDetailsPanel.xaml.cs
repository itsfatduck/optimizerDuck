using System.Windows.Media;
using optimizerDuck.Resources.Languages;
using Wpf.Ui.Controls;
using StartupTask = optimizerDuck.Domain.Optimizations.Models.StartupManager.StartupTask;

namespace optimizerDuck.UI.Dialogs;

public partial class StartupTaskDetailsPanel : System.Windows.Controls.UserControl
{
    public StartupTaskDetailsPanel(StartupTask task)
    {
        InitializeComponent();

        DataContext = task;

        TaskStateText.Text = task.IsEnabled
            ? Translations.Common_Toggle_On
            : Translations.Common_Toggle_Off;
        var brushKey = task.IsEnabled
            ? "SystemFillColorSuccessBrush"
            : "SystemFillColorCriticalBrush";
        if (TryFindResource(brushKey) is Brush brush)
            TaskStateBadge.Background = brush;
    }
}
