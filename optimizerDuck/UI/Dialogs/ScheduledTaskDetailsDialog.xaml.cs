using System.Windows.Controls;
using ScheduledTaskModel = optimizerDuck.Domain.Optimizations.Models.ScheduledTask.ScheduledTaskModel;

namespace optimizerDuck.UI.Dialogs;

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
            DataContext = value;
        }
    }
}
