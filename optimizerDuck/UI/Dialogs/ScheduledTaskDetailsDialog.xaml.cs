using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;
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
            if (value == null)
                return;

            TaskLogoImage.Source = value.LogoImage;
            TaskNameText.Text = value.Name;
            PathText.Text = value.FullPath;
            DescriptionText.Text = value.Description ?? "—";
            AuthorText.Text = value.Author ?? "—";
            StateText.Text = value.State;
            TriggersText.Text = string.IsNullOrWhiteSpace(value.TriggerSummary)
                ? "—"
                : value.TriggerSummary;
            ActionText.Text = string.IsNullOrWhiteSpace(value.ActionSummary)
                ? "—"
                : value.ActionSummary;
            LastRunText.Text = value.LastRunTime?.ToString("g") ?? "—";
            NextRunText.Text = value.NextRunTime?.ToString("g") ?? "—";
            LastResultText.Text = value.LastRunResult?.ToString() ?? "—";
            TriggerBadgesItems.ItemsSource = value.TriggerTypes;

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
