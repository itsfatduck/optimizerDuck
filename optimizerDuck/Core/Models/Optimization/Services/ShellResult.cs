namespace optimizerDuck.Core.Models.Optimization.Services;

public record ShellResult(
    string Command,
    string Stdout,
    string Stderr,
    int ExitCode,
    TimeSpan Duration);