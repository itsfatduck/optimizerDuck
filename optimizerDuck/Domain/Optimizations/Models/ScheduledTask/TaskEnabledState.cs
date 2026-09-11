namespace optimizerDuck.Domain.Optimizations.Models.ScheduledTask;

/// <summary>Outcome of reading a scheduled task's enabled state.</summary>
public enum TaskEnabledState
{
    Enabled,

    Disabled,

    NotFound,

    Unknown,
}
