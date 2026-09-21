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
    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private ImageSource? _logoImage;

    public required string TaskName { get; init; }

    public required string TaskPath { get; init; }

    public string? Description { get; init; }

    /// <summary>
    ///     Display data of the task's triggers (labels re-resolve on language change).
    /// </summary>
    public IReadOnlyList<ScheduledTaskTriggerInfo> TriggerInfos { get; init; } = [];

    /// <summary>
    ///     Badges for the task's triggers (e.g. "At log on", "At startup"), each carrying the
    ///     localized detail the chip tooltip shows.
    /// </summary>
    public IReadOnlyList<ScheduledTaskTriggerBadge> TriggerBadges =>
        TriggerInfos.Select(t => new ScheduledTaskTriggerBadge(t.Label, t.Detail)).ToList();

    public string TriggerSummary => string.Join("; ", TriggerInfos.Select(t => t.Detail));

    public string? ActionSummary { get; init; }

    public bool IsMicrosoftTask =>
        TaskPath.StartsWith(@"\Microsoft\", StringComparison.OrdinalIgnoreCase);

    public bool IsDescriptionEmpty => string.IsNullOrWhiteSpace(Description);
    public bool IsTriggersEmpty => string.IsNullOrWhiteSpace(TriggerSummary);
    public bool IsActionEmpty => string.IsNullOrWhiteSpace(ActionSummary);
}
