namespace optimizerDuck.Domain.Optimizations.Models.Services;

/// <summary>
///     Represents the result of a shell command execution.
/// </summary>
public record ShellResult
{
    /// <summary>
    ///     The full command string that was executed.
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    ///     The standard output captured from the process.
    /// </summary>
    public required string Stdout { get; init; }

    /// <summary>
    ///     The standard error output captured from the process.
    /// </summary>
    public required string Stderr { get; init; }

    /// <summary>
    ///     The process exit code. A value of <c>-1</c> indicates a timeout,
    ///     and <c>-2</c> indicates an exception during execution.
    /// </summary>
    public required int ExitCode { get; init; }

    /// <summary>
    ///     The total wall-clock duration of the command execution.
    /// </summary>
    public required TimeSpan Duration { get; init; }
}