namespace optimizerDuck.Domain.Execution;

/// <summary>
///     The subjects of the built-in tools. An identifier is a permanent record key, so changing one
///     orphans the records already written under it.
/// </summary>
public static class ToolSubjects
{
    /// <summary>The scheduled task manager on the tools page.</summary>
    public static readonly OperationSubject ScheduledTasks = new(
        Guid.Parse("9F1C4A2E-7B3D-4E58-9A61-0D2F5C8B7E14"),
        "ScheduledTasks",
        "Scheduled task"
    );

    /// <summary>
    ///     The startup manager's scheduled-task list. Its toggles are reported as its own runs, not
    ///     as the task manager's.
    /// </summary>
    public static readonly OperationSubject StartupManagerTasks = new(
        Guid.Parse("34E4A189-58FA-418A-AEFB-9D384C4D586B"),
        "StartupManagerTasks",
        "Startup manager task"
    );
}
