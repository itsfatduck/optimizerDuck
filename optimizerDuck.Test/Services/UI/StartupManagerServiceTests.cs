using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.StartupManager;
using optimizerDuck.Services.UI;

namespace optimizerDuck.Test.Services.UI;

public class StartupManagerServiceTests
{
    [Fact]
    public async Task ToggleStartupApp_UnsupportedLocation_ReturnsFailure()
    {
        var service = new StartupManagerService(NullLogger<StartupManagerService>.Instance);
        var app = new StartupApp
        {
            Name = "Test App",
            Location = StartupAppLocation.RegistryHKCURun,
            PathOrKey = "NoBackslashHere",
        };

        var result = await service.ToggleStartupApp(app, true);

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ToggleStartupTask_MissingTask_ReturnsFailure()
    {
        // A toggle for a task that does not exist must report failure, never log success.
        var service = new StartupManagerService(NullLogger<StartupManagerService>.Instance);
        var call = new OpCall { Changes = new ChangeSet(), Logger = NullLogger.Instance };
        var task = new StartupTask
        {
            TaskName = $"optimizerDuck_Test_Missing_{Guid.NewGuid():N}",
            TaskPath = "\\",
        };

        var result = await service.ToggleStartupTask(call, task, true);

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);

        // The provider recorded what it found, and the service decided this list cannot act on it.
        Assert.Equal(ChangeKind.NotApplicable, Assert.Single(call.Changes.Changes).Kind);
    }
}
