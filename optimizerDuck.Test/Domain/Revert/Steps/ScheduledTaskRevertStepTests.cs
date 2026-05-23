using optimizerDuck.Domain.Revert.Steps;

namespace optimizerDuck.Test.Domain.Revert.Steps;

public class ScheduledTaskRevertStepTests
{
    [Fact]
    public async Task ExecuteAsync_WithMissingTask_ReturnsFalse()
    {
        var step = new ScheduledTaskRevertStep
        {
            FullPath = @"\NonExistent\OptimizerDuckTestTask",
            OriginalEnabled = true,
        };

        var success = await step.ExecuteAsync();

        Assert.False(success);
    }
}
