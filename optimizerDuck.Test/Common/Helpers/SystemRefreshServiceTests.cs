using optimizerDuck.Common.Helpers;

namespace optimizerDuck.Test.Common.Helpers;

/// <summary>
/// Integration-style smoke tests for <see cref="SystemRefreshService"/>.
/// These call the actual Win32 API — they verify the P/Invoke signatures
/// are correct and the service doesn't throw.
/// </summary>
public class SystemRefreshServiceTests
{
    [Fact]
    public void NotifySettingChange_DoesNotThrow()
    {
        var exception = Record.Exception(() => SystemRefreshService.NotifySettingChange());
        Assert.Null(exception);
    }

    [Fact]
    public void RefreshShell_DoesNotThrow()
    {
        var exception = Record.Exception(() => SystemRefreshService.RefreshShell());
        Assert.Null(exception);
    }

    [Fact]
    public void NotifySettingChange_ThenRefreshShell_DoesNotThrow()
    {
        // Simulate the call sequence ExecutePostActionAsync would make
        var exception1 = Record.Exception(() => SystemRefreshService.NotifySettingChange());
        Assert.Null(exception1);

        var exception2 = Record.Exception(() => SystemRefreshService.RefreshShell());
        Assert.Null(exception2);
    }

    [Fact]
    public void NotifySettingChange_CanBeCalledMultipleTimes()
    {
        for (var i = 0; i < 5; i++)
        {
            var exception = Record.Exception(() => SystemRefreshService.NotifySettingChange());
            Assert.Null(exception);
        }
    }
}
