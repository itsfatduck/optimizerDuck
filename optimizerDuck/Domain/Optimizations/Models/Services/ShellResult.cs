namespace optimizerDuck.Domain.Optimizations.Models.Services;

public record ShellResult
{
    public required string Command { get; init; }

    public required string Stdout { get; init; }

    public required string Stderr { get; init; }

    /// <summary>
    ///     The process exit code. A value of <c>-1</c> indicates a timeout,
    ///     and <c>-2</c> indicates an exception during execution.
    /// </summary>
    public required int ExitCode { get; init; }

    public required TimeSpan Duration { get; init; }
}
