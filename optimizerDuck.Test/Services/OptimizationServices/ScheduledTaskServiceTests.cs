using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Domain.Optimizations.Models.ScheduledTask;
using optimizerDuck.Services.Optimization.Providers;

namespace optimizerDuck.Test.Services.OptimizationServices;

public class ScheduledTaskServiceTests
{
    [Fact]
    public void GetTaskEnabledState_NonExistentRootTask_ReportsNotFound()
    {
        // Under the always-present root folder a missing task must be reported as
        // "not found" rather than "disabled" or "query failed".
        var path = $"\\optimizerDuck_Test_Missing_{Guid.NewGuid():N}";

        var state = ScheduledTaskService.GetTaskEnabledState(path, NullLogger.Instance);

        Assert.Equal(TaskEnabledState.NotFound, state);
    }
}
