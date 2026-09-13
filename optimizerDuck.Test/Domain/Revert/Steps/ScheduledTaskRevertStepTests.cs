using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using optimizerDuck.Domain.Exceptions;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Test.TestDoubles;

namespace optimizerDuck.Test.Domain.Revert.Steps;

public class ScheduledTaskRevertStepTests
{
    [Fact]
    public async Task ExecuteAsync_WithMissingTask_ThrowsStepExecutionException()
    {
        var step = new ScheduledTaskRevertStep
        {
            FullPath = @"\NonExistent\OptimizerDuckTestTask",
            OriginalEnabled = true,
        };

        var ex = await Assert.ThrowsAsync<StepExecutionException>(() =>
            step.ExecuteAsync(TestShell.Context(), NullLogger.Instance)
        );

        Assert.Contains("NonExistent", ex.Message);
    }

    [Fact]
    public void FromData_MissingOriginalEnabled_ThrowsFailClosed()
    {
        var data = new JObject { ["FullPath"] = @"\Test\Task" };

        var ex = Assert.Throws<StepExecutionException>(() =>
            ScheduledTaskRevertStep.FromData(data)
        );

        Assert.Contains(nameof(ScheduledTaskRevertStep.OriginalEnabled), ex.Message);
    }
}
