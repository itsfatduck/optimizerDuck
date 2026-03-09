using System.Windows.Controls;
using optimizerDuck.Core.Models.Optimization.StartupManager;
using StartupTask = optimizerDuck.Core.Models.Optimization.StartupManager.StartupTask;

namespace optimizerDuck.UI.Views.Dialogs;

public partial class StartupTaskDetailsPanel : UserControl
{
    public StartupTaskDetailsPanel(StartupTask task)
    {
        InitializeComponent();

        NameText.Text = task.TaskName;
        DescriptionText.Text = task.Description ?? "—";
        PathText.Text = task.TaskPath;
        TriggersText.Text = task.TriggerSummary ?? "—";
        ActionText.Text = task.ActionSummary ?? "—";
    }
}