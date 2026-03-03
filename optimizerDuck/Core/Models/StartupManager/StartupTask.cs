using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace optimizerDuck.Core.Models.StartupManager;

/// <summary>
///     Represents a scheduled task that runs at startup.
/// </summary>
public partial class StartupTask : ObservableObject
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
    ///     Summary of when the task triggers.
    /// </summary>
    public string? TriggerSummary { get; init; }
    /// <summary>
    ///     The types of triggers (e.g., "At logon", "At startup").
    /// </summary>
    public List<string> TriggerTypes { get; init; } = [];
    /// <summary>
    ///     Summary of what the task does.
    /// </summary>
    public string? ActionSummary { get; init; }

    /// <summary>
    ///     Indicates whether this is a Microsoft system task.
    /// </summary>
    public bool IsMicrosoftTask => TaskPath.StartsWith(@"\Microsoft\", StringComparison.OrdinalIgnoreCase);
}