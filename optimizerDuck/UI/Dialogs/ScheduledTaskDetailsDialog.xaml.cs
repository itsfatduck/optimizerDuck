using System.Windows.Media;
using Wpf.Ui.Controls;
using ScheduledTaskModel = optimizerDuck.Domain.Optimizations.Models.ScheduledTask.ScheduledTaskModel;

namespace optimizerDuck.UI.Dialogs;

public partial class ScheduledTaskDetailsDialog : System.Windows.Controls.UserControl
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
            if (value == null)
                return;

            DataContext = value;

            var backgroundBrushKey = value.State switch
            {
                "Ready" => "SystemFillColorSuccessBrush",
                "Running" => "AccentButtonBackground",
                "Disabled" => "SystemFillColorCautionBrush",
                _ => "CardBackgroundFillColorSecondaryBrush",
            };

            var iconSymbol = value.State switch
            {
                "Ready" => SymbolRegular.CheckmarkCircle24,
                "Running" => SymbolRegular.Play24,
                "Disabled" => SymbolRegular.Dismiss24,
                _ => SymbolRegular.Circle24,
            };

            var iconBrushKey = value.State switch
            {
                "Ready" => "TextFillColorInverseBrush",
                "Running" => "TextFillColorInverseBrush",
                "Disabled" => "TextFillColorInverseBrush",
                _ => "TextFillColorPrimaryBrush",
            };

            if (TryFindResource(backgroundBrushKey) is Brush backgroundBrush)
                StateBorder.Background = backgroundBrush;

            StateIcon.Symbol = iconSymbol;

            if (TryFindResource(iconBrushKey) is Brush iconBrush)
                StateIcon.Foreground = iconBrush;
        }
    }
}
