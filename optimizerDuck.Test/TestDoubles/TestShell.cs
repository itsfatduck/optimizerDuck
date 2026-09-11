using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Services.Optimization.Providers;

namespace optimizerDuck.Test.TestDoubles;

/// <summary>Builds a <see cref="ShellService"/> with a fixed timeout for tests that need one.</summary>
internal static class TestShell
{
    public const int TimeoutMs = 120_000;

    public static ShellService New() =>
        new(new ProcessRunner(TimeoutMs, NullLogger<ProcessRunner>.Instance));
}
