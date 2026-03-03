using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace optimizerDuck.Core.Models.ScheduledTask;

public partial class ScheduledTaskModel : ObservableObject
{
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private string _state = string.Empty;
    [ObservableProperty] private ImageSource? _logoImage;

    public required string Name { get; init; }
    public required string Path { get; init; }
    public required string FullPath { get; init; }
    public string? Description { get; init; }
    public string? Author { get; init; }

    public string TriggerSummary { get; init; } = string.Empty;
    public string ActionSummary { get; init; } = string.Empty;

    /// <summary>Individual trigger type strings for badge display (e.g. "At log on", "At startup", "Daily at 09:00").</summary>
    public ObservableCollection<string> TriggerTypes { get; init; } = [];

    public DateTime? LastRunTime { get; init; }
    public DateTime? NextRunTime { get; init; }
    public int? LastRunResult { get; init; }

    public bool IsMicrosoftTask => Path.StartsWith(@"\Microsoft\", StringComparison.OrdinalIgnoreCase);
    public bool HasLogonTrigger { get; init; }
    public bool HasBootTrigger { get; init; }

    // New expanded creation properties
    public string ExecutablePath { get; init; } = string.Empty;
    public string Arguments { get; init; } = string.Empty;
    public bool HasIdleTrigger { get; init; }
    public bool HasRegistrationTrigger { get; init; }
    public bool HasDailyTrigger { get; init; }
    public TimeSpan DailyTriggerTime { get; init; }
    public bool RunWithHighestPrivileges { get; init; }
    public bool Hidden { get; init; }

    // Computed state helpers for UI binding
    public bool IsRunning => State.Equals("Running", StringComparison.OrdinalIgnoreCase);
    public bool IsReady => State.Equals("Ready", StringComparison.OrdinalIgnoreCase);
    public bool IsDisabledState => State.Equals("Disabled", StringComparison.OrdinalIgnoreCase);

    /// <summary>Notifies UI that computed state properties have changed.</summary>
    public void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(IsDisabledState));
    }
}
