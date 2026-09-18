using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Services.Optimization;
using optimizerDuck.Services.Revert;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Test.TestDoubles;

/// <summary>The run lifecycle as the application's graph builds it, logging nothing.</summary>
internal static class TestRunner
{
    /// <summary>Builds a run lifecycle over the given revert store.</summary>
    /// <param name="revertManager">The revert store the runs persist into.</param>
    /// <param name="logger">The logger the runs report to, or nothing when omitted.</param>
    /// <returns>The run lifecycle the application's graph would inject.</returns>
    internal static OperationRunner New(
        RevertManager revertManager,
        ILogger<OperationRunner>? logger = null
    )
    {
        return new OperationRunner(
            revertManager,
            new SystemInfoService(NullLogger<SystemInfoService>.Instance),
            new StreamService(NullLogger<StreamService>.Instance),
            new ShellService(new ProcessRunner(120_000)),
            new PowerPlanService(NullLogger<PowerPlanService>.Instance),
            logger ?? NullLogger<OperationRunner>.Instance
        );
    }
}
