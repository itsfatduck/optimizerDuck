using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Services;

namespace optimizerDuck.Test.Services;

public class SystemInfoServiceTests
{
    [Fact]
    public async Task RefreshAsync_SetsSnapshot()
    {
        var service = new SystemInfoService(NullLogger<SystemInfoService>.Instance);

        var snapshot = await service.RefreshAsync();

        Assert.Equal(snapshot, service.Snapshot);
    }

    [Fact]
    public void LogSummary_DoesNotThrow()
    {
        var service = new SystemInfoService(NullLogger<SystemInfoService>.Instance);

        service.LogSummary();
    }
}