using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Services;

namespace optimizerDuck.Test.Services;

public class SystemInfoServiceTests
{
    [Fact]
    public async Task RefreshAsync_SetsSnapshot()
    {
        var service = new SystemInfoService(NullLogger<SystemInfoService>.Instance);
        var cancellationToken = TestContext.Current.CancellationToken;

        var snapshot = await service.RefreshAsync(cancellationToken);

        Assert.Equal(snapshot, service.Snapshot);
    }

    [Fact]
    public void LogSummary_DoesNotThrow()
    {
        var service = new SystemInfoService(NullLogger<SystemInfoService>.Instance);

        service.LogSummary();
    }
}
