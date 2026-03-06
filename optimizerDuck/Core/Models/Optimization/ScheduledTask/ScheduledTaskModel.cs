using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace optimizerDuck.Core.Models.ScheduledTask;

/// <summary>
///     Represents a Windows Scheduled Task.
/// </summary>
public partial class ScheduledTaskModel : ObservableObject
{
    /// <summary>
    ///     Indicates whether the task is enabled.
    /// </summary>
    [ObservableProperty] private bool _isEnabled;

    /// <summary>
    ///     The logo image of the task's executable.
    /// </summary>
    [ObservableProperty] private ImageSource? _logoImage;

    /// <summary>
    ///     The current state of the task (e.g., "Ready", "Running", "Disabled").
    /// </summary>
    [ObservableProperty] private string _state = string.Empty;

    /// <summary>
    ///     The name of the task.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     The folder path of the task.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    ///     The full path to the task.
    /// </summary>
    public required string FullPath { get; init; }

    /// <summary>
    ///     Description of the task.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    ///     The author of the task.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    ///     Summary of when the task triggers.
    /// </summary>
    public string TriggerSummary { get; init; } = string.Empty;

    /// <summary>
    ///     Summary of the task's action.
    /// </summary>
    public string ActionSummary { get; init; } = string.Empty;

    /// <summary>
    ///     Individual trigger type strings for badge display (e.g. "At log on", "At startup", "Daily at 09:00").
    /// </summary>
    public ObservableCollection<string> TriggerTypes { get; init; } = [];

    /// <summary>
    ///     The last time the task ran.
    /// </summary>
    public DateTime? LastRunTime { get; init; }

    /// <summary>
    ///     The next scheduled run time.
    /// </summary>
    public DateTime? NextRunTime { get; init; }

    /// <summary>
    ///     The result code of the last run.
    /// </summary>
    public int? LastRunResult { get; init; }

    /// <summary>
    ///     Indicates whether this is a Microsoft system task.
    /// </summary>
    public bool IsMicrosoftTask => Path.StartsWith(@"\Microsoft\", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Indicates whether the task has a logon trigger.
    /// </summary>
    public bool HasLogonTrigger { get; init; }

    /// <summary>
    ///     Indicates whether the task has a boot trigger.
    /// </summary>
    public bool HasBootTrigger { get; init; }

    // New expanded creation properties
    /// <summary>
    ///     The executable path for the task's action.
    /// </summary>
    public string ExecutablePath { get; init; } = string.Empty;

    /// <summary>
    ///     Arguments passed to the executable.
    /// </summary>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>
    ///     Indicates whether the task has an idle trigger.
    /// </summary>
    public bool HasIdleTrigger { get; init; }

    /// <summary>
    ///     Indicates whether the task has a registration trigger.
    /// </summary>
    public bool HasRegistrationTrigger { get; init; }

    /// <summary>
    ///     Indicates whether the task has a daily trigger.
    /// </summary>
    public bool HasDailyTrigger { get; init; }

    /// <summary>
    ///     The time of day for daily triggers.
    /// </summary>
    public TimeSpan DailyTriggerTime { get; init; }

    /// <summary>
    ///     Indicates whether the task runs with the highest privileges.
    /// </summary>
    public bool RunWithHighestPrivileges { get; init; }

    /// <summary>
    ///     Indicates whether the task is hidden.
    /// </summary>
    public bool Hidden { get; init; }

    // Computed state helpers for UI binding
    /// <summary>
    ///     Indicates whether the task is currently running.
    /// </summary>
    public bool IsRunning => State.Equals("Running", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Indicates whether the task is ready to run.
    /// </summary>
    public bool IsReady => State.Equals("Ready", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Indicates whether the task is in a disabled state.
    /// </summary>
    public bool IsDisabledState => State.Equals("Disabled", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Notifies UI that computed state properties have changed.
    /// </summary>
    public void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(IsDisabledState));
    }
}