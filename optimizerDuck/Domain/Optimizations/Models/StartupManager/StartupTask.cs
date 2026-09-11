using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Optimizations.Models.ScheduledTask;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Domain.Optimizations.Models.StartupManager;

/// <summary>
///     Represents a scheduled task that runs at startup.
/// </summary>
public partial class StartupTask : LocalizedObject
{
    /// <summary>
    ///     Indicates whether the task is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isEnabled;

    /// <summary>
    ///     The logo image of the task's executable.
    /// </summary>
    [ObservableProperty]
    private ImageSource? _logoImage;

    /// <summary>
    ///     The name of the scheduled task.
    /// </summary>
    public required string TaskName { get; init; }

    /// <summary>
    ///     The path to the task in Task Scheduler.
    /// </summary>
    public required string TaskPath { get; init; }

    /// <summary>
    ///     Description of the task.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    ///     Display data of the task's triggers (labels re-resolve on language change).
    /// </summary>
    public IReadOnlyList<ScheduledTaskTriggerInfo> TriggerInfos { get; init; } = [];

    /// <summary>
    ///     Localized trigger labels for badge display (e.g. "At log on", "At startup").
    /// </summary>
    public IReadOnlyList<string> TriggerTypes => TriggerInfos.Select(t => t.Label).ToList();

    /// <summary>
    ///     Localized summary of when the task triggers.
    /// </summary>
    public string TriggerSummary => string.Join("; ", TriggerInfos.Select(t => t.Detail));

    /// <summary>
    ///     Summary of what the task does.
    /// </summary>
    public string? ActionSummary { get; init; }

    /// <summary>
    ///     Indicates whether this is a Microsoft system task.
    /// </summary>
    public bool IsMicrosoftTask =>
        TaskPath.StartsWith(@"\Microsoft\", StringComparison.OrdinalIgnoreCase);

    public bool IsDescriptionEmpty => string.IsNullOrWhiteSpace(Description);
    public bool IsTriggersEmpty => string.IsNullOrWhiteSpace(TriggerSummary);
    public bool IsActionEmpty => string.IsNullOrWhiteSpace(ActionSummary);
}
