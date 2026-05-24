using optimizerDuck.Domain.Exceptions;
using optimizerDuck.Domain.Revert.Steps;

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

        var ex = await Assert.ThrowsAsync<StepExecutionException>(() => step.ExecuteAsync());

        Assert.Contains("NonExistent", ex.Message);
    }
}
