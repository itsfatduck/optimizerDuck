using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Test.TestDoubles;

namespace optimizerDuck.Test.Domain.Revert.Steps;

public class ServiceRevertStepTests
{
    [Fact]
    public async Task ExecuteAsync_MissingService_ReturnsTrueWithoutThrowing()
    {
        // A service that no longer exists has nothing to restore: revert must
        // succeed instead of throwing on the unverifiable query.
        var step = new ServiceRevertStep
        {
            ServiceName = $"optimizerDuck_Test_Missing_{Guid.NewGuid():N}",
            OriginalStartupType = ServiceStartupType.Manual,
        };

        var result = await step.ExecuteAsync(TestShell.Context(), NullLogger.Instance);

        Assert.True(result);
    }
}
