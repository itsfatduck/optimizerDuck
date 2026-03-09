using System.Windows.Controls;
using optimizerDuck.Core.Models.Optimization.ScheduledTask;
using ScheduledTaskModel = optimizerDuck.Core.Models.Optimization.ScheduledTask.ScheduledTaskModel;

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

            var folderPath = FolderPathBox.Text?.Trim() ?? "\\";
            if (!folderPath.StartsWith("\\")) folderPath = "\\" + folderPath;
            if (folderPath.Length > 1) folderPath = folderPath.TrimEnd('\\');

            var fullPath = folderPath == "\\" ? $"\\{name}" : $"{folderPath}\\{name}";

            // Parse daily time
            var time = TimeSpan.Zero;
            if (DailyTriggerCheck.IsChecked == true && !string.IsNullOrWhiteSpace(DailyTimeBox.Text))
                if (TimeSpan.TryParse(DailyTimeBox.Text.Trim(), out var parsedTime))
                    time = parsedTime;

            return new ScheduledTaskModel
            {
                Name = name,
                Path = folderPath,
                FullPath = fullPath,
                Description = DescriptionBox.Text?.Trim(),
                ExecutablePath = ExecutableBox.Text?.Trim() ?? string.Empty,
                Arguments = ArgumentsBox.Text?.Trim() ?? string.Empty,
                IsEnabled = EnabledCheck.IsChecked == true,
                HasLogonTrigger = LogonTriggerCheck.IsChecked == true,
                HasBootTrigger = BootTriggerCheck.IsChecked == true,
                HasIdleTrigger = IdleTriggerCheck.IsChecked == true,
                HasRegistrationTrigger = RegistrationTriggerCheck.IsChecked == true,
                HasDailyTrigger = DailyTriggerCheck.IsChecked == true,
                DailyTriggerTime = time,
                RunWithHighestPrivileges = HighestPrivilegesCheck.IsChecked == true,
                Hidden = HiddenCheck.IsChecked == true
            };
        }
    }
}