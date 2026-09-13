using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Test.TestDoubles;

/// <summary>Builds a <see cref="ShellService"/> with a fixed timeout for tests that need one.</summary>
internal static class TestShell
{
    public const int TimeoutMs = 120_000;

    public static ShellService New() =>
        new(new ProcessRunner(TimeoutMs, NullLogger<ProcessRunner>.Instance));

    public static RevertContext Context(ILogger? logger = null) =>
        new()
        {
            Shell = New(),
            PowerPlans = new PowerPlanService(NullLogger<PowerPlanService>.Instance),
            Logger = logger ?? NullLogger.Instance,
        };
}
