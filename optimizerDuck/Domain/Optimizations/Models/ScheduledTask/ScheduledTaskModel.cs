using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Domain.Optimizations.Models.ScheduledTask;

/// <summary>
///     Represents a Windows Scheduled Task.
/// </summary>
public partial class ScheduledTaskModel : LocalizedObject
{
    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private ImageSource? _logoImage;

    /// <summary>
    ///     The raw Windows state, e.g. "Ready", "Running" or "Disabled".
    /// </summary>
    [ObservableProperty]
    private string _state = string.Empty;

    /// <summary>
    ///     The localized state label. Raw Windows states ("Ready", "Running", ...) are
    ///     mapped through translations and unknown values are shown as-is.
    /// </summary>
    public string StateDisplay =>
        State?.ToLowerInvariant() switch
        {
            "running" => Loc.Instance["ScheduledTasks.State.Running"],
            "ready" => Loc.Instance["ScheduledTasks.State.Ready"],
            "disabled" => Loc.Instance["ScheduledTasks.State.Disabled"],
            "queued" => Loc.Instance["ScheduledTasks.State.Queued"],
            _ => State ?? string.Empty,
        };

    public required string Name { get; init; }

    public required string Path { get; init; }

    public required string FullPath { get; init; }

    public string? Description { get; init; }

    public string? Author { get; init; }

    /// <summary>
    ///     Display data of the task's triggers. Badges show
    ///     <see cref="ScheduledTaskTriggerInfo.Label"/>; the summary joins
    ///     <see cref="ScheduledTaskTriggerInfo.Detail"/>.
    /// </summary>
    public IReadOnlyList<ScheduledTaskTriggerInfo> TriggerInfos { get; init; } = [];

    /// <summary>
    ///     Localized trigger labels for badge display (e.g. "At log on", "Daily").
    /// </summary>
    public IReadOnlyList<string> TriggerTypes => TriggerInfos.Select(t => t.Label).ToList();

    /// <summary>
    ///     Localized summary of when the task triggers, keeping each trigger's data.
    /// </summary>
    public string TriggerSummary => string.Join("; ", TriggerInfos.Select(t => t.Detail));

    public string ActionSummary { get; init; } = string.Empty;

    public DateTime? LastRunTime { get; init; }

    public DateTime? NextRunTime { get; init; }

    public int? LastRunResult { get; init; }

    public bool IsMicrosoftTask =>
        Path.StartsWith(@"\Microsoft\", StringComparison.OrdinalIgnoreCase);

    public bool HasLogonTrigger { get; init; }

    public bool HasBootTrigger { get; init; }

    public string ExecutablePath { get; init; } = string.Empty;

    public string Arguments { get; init; } = string.Empty;

    public bool HasIdleTrigger { get; init; }

    public bool HasRegistrationTrigger { get; init; }

    public bool HasDailyTrigger { get; init; }

    public TimeSpan DailyTriggerTime { get; init; }

    public bool RunWithHighestPrivileges { get; init; }

    public bool Hidden { get; init; }

    public bool IsRunning => State.Equals("Running", StringComparison.OrdinalIgnoreCase);

    public bool IsReady => State.Equals("Ready", StringComparison.OrdinalIgnoreCase);

    public bool IsDisabledState => State.Equals("Disabled", StringComparison.OrdinalIgnoreCase);

    /// <summary>Raises change notifications for the computed state properties.</summary>
    public void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(IsDisabledState));
    }

    public bool IsDescriptionEmpty => string.IsNullOrWhiteSpace(Description);
    public bool IsAuthorEmpty => string.IsNullOrWhiteSpace(Author);
    public bool IsTriggersEmpty => string.IsNullOrWhiteSpace(TriggerSummary);
    public bool IsActionEmpty => string.IsNullOrWhiteSpace(ActionSummary);
    public bool IsLastRunEmpty => LastRunTime is null;
    public bool IsNextRunEmpty => NextRunTime is null;
    public bool IsLastResultEmpty => LastRunResult is null;
}
