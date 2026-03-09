using System.Windows.Controls;
using System.Windows.Media;
using optimizerDuck.Core.Models.Optimization.ScheduledTask;
using ScheduledTaskModel = optimizerDuck.Core.Models.Optimization.ScheduledTask.ScheduledTaskModel;

namespace optimizerDuck.UI.Views.Dialogs;

public partial class ScheduledTaskDetailsDialog : UserControl
{
    private ScheduledTaskModel? _taskModel;

    public ScheduledTaskDetailsDialog()
    {
        InitializeComponent();
    }

    public ScheduledTaskModel? TaskModel
    {
        get => _taskModel;
        set
        {
            _taskModel = value;
            if (value == null) return;

            PathText.Text = value.FullPath;
            DescriptionText.Text = value.Description ?? "—";
            AuthorText.Text = value.Author ?? "—";
            StateText.Text = value.State;
            TriggersText.Text = string.IsNullOrWhiteSpace(value.TriggerSummary) ? "—" : value.TriggerSummary;
            ActionText.Text = string.IsNullOrWhiteSpace(value.ActionSummary) ? "—" : value.ActionSummary;
            LastRunText.Text = value.LastRunTime?.ToString("g") ?? "—";
            NextRunText.Text = value.NextRunTime?.ToString("g") ?? "—";
            LastResultText.Text = value.LastRunResult?.ToString() ?? "—";

            // Set state badge color
            var brushKey = value.State switch
            {
                "Ready" => "SystemFillColorSuccessBrush",
                "Running" => "AccentButtonBackground",
                "Disabled" => "SystemFillColorCautionBrush",
                _ => "CardBackgroundFillColorSecondaryBrush"
            };

            if (TryFindResource(brushKey) is Brush brush)
                StateBorder.Background = brush;
        }
    }
}