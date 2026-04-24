using System.Windows.Controls;
using System.Windows.Media;
using optimizerDuck.Resources.Languages;
using StartupTask = optimizerDuck.Domain.Optimizations.Models.StartupManager.StartupTask;

namespace optimizerDuck.UI.Dialogs;

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
        LogoImageControl.Source = task.LogoImage;
        TriggerBadgesItems.ItemsSource = task.TriggerTypes;

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
