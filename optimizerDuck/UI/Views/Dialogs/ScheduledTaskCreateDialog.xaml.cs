using System.Windows.Controls;
using optimizerDuck.Core.Models.ScheduledTask;

namespace optimizerDuck.UI.Views.Dialogs;

public partial class ScheduledTaskCreateDialog : UserControl
{
    public ScheduledTaskCreateDialog()
    {
        InitializeComponent();
    }

    public ScheduledTaskModel? CreatedModel
    {
        get
        {
            var name = TaskNameBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name)) return null;

            return new ScheduledTaskModel
            {
                Name = name,
                Path = FolderPathBox.Text?.Trim() ?? "\\",
                FullPath = (FolderPathBox.Text?.Trim()?.TrimEnd('\\') ?? "\\") + "\\" + name,
                Description = DescriptionBox.Text?.Trim(),
                ActionSummary = ExecutableBox.Text?.Trim() ?? string.Empty,
                IsEnabled = EnabledCheck.IsChecked == true,
                HasLogonTrigger = LogonTriggerCheck.IsChecked == true,
                HasBootTrigger = BootTriggerCheck.IsChecked == true
            };
        }
    }
}
