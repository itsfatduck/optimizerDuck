using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using optimizerDuck.Domain.Exceptions;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Test.TestDoubles;

namespace optimizerDuck.Test.Domain.Revert.Steps;

public class ScheduledTaskRevertStepTests
{
    [Fact]
    public async Task ExecuteAsync_WithMissingTask_SucceedsBecauseThereIsNothingToRestore()
    {
        // The provider reports a missing task as nothing to change, so the step reaches the
        // branch it was written for: a task that no longer exists has nothing to restore.
        var step = new ScheduledTaskRevertStep
        {
            FullPath = @"\NonExistent\OptimizerDuckTestTask",
            OriginalEnabled = true,
        };

        Assert.True(await step.ExecuteAsync(TestShell.Context(), NullLogger.Instance));
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
