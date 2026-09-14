using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Services.System;

namespace optimizerDuck.Test.Services;

/// <summary>
///     System Restore decisions with hand-doubled results. Classification takes status codes only
///     (never message text, which is localizable) and the throttle takes the documented setting plus
///     the newest existing point. The live create/enable round trip needs elevation, so the virtual
///     members are driven through a hand-written double - the same seam a restore-point manager tool
///     would use.
/// </summary>
public class SystemRestoreServiceTests
{
    private static readonly DateTime Now = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    private sealed class FakeSystemRestoreService : SystemRestoreService
    {
        public FakeSystemRestoreService()
            : base(NullLogger<SystemRestoreService>.Instance) { }

        public IReadOnlyList<RestorePointInfo> Points { get; init; } = [];

        public int FrequencyMinutes { get; init; } = DefaultCreationFrequencyMinutes;

        public SystemRestoreResult CreateResult { get; init; } = new(true, 0);

        public SystemRestoreResult EnableResult { get; init; } = new(true, 0);

        public string? EnabledDrive { get; private set; }

        public override IReadOnlyList<RestorePointInfo> ListRestorePoints() => Points;

        public override int GetCreationFrequencyMinutes() => FrequencyMinutes;

        public override SystemRestoreResult CreateRestorePoint(
            string description,
            uint restorePointType = RestorePointTypeModifySettings,
            uint eventType = EventTypeBeginSystemChange
        ) => CreateResult;

        public override SystemRestoreResult EnableProtection(string drive)
        {
            EnabledDrive = drive;
            return EnableResult;
        }
    }

    [Fact]
    public void IsWithinCreationThrottle_PointInsideWindow_IsThrottled()
    {
        Assert.True(
            SystemRestoreService.IsWithinCreationThrottle(
                SystemRestoreService.DefaultCreationFrequencyMinutes,
                Now.AddHours(-1),
                Now
            )
        );
    }

    [Fact]
    public void IsWithinCreationThrottle_PointOutsideWindow_IsNotThrottled()
    {
        Assert.False(
            SystemRestoreService.IsWithinCreationThrottle(
                SystemRestoreService.DefaultCreationFrequencyMinutes,
                Now.AddHours(-25),
                Now
            )
        );
    }

    [Fact]
    public void IsWithinCreationThrottle_ExactlyAtBoundary_IsNotThrottled()
    {
        // Windows skips only points created strictly inside the window.
        Assert.False(SystemRestoreService.IsWithinCreationThrottle(60, Now.AddMinutes(-60), Now));
    }

    [Fact]
    public void IsWithinCreationThrottle_FrequencyZero_NeverSkips()
    {
        Assert.False(SystemRestoreService.IsWithinCreationThrottle(0, Now.AddSeconds(-1), Now));
    }

    [Fact]
    public void IsWithinCreationThrottle_UnknownNewestPoint_FailsOpen()
    {
        Assert.False(SystemRestoreService.IsWithinCreationThrottle(1440, null, Now));
    }

    [Fact]
    public void DefaultCreationFrequencyMinutes_IsTheDocumentedTwentyFourHours()
    {
        Assert.Equal(1440, SystemRestoreService.DefaultCreationFrequencyMinutes);
    }

    [Theory]
    [InlineData(0u, false)] // success
    [InlineData(1058u, true)] // ERROR_SERVICE_DISABLED
    [InlineData(0x80070422u, true)] // HRESULT_FROM_WIN32(ERROR_SERVICE_DISABLED)
    [InlineData(5u, false)] // ERROR_ACCESS_DENIED: a refusal, not "protection off"
    [InlineData(0x80070005u, false)] // E_ACCESSDENIED
    [InlineData(uint.MaxValue, false)] // the interop threw
    public void IsProtectionDisabledStatus_OnlyMatchesTheDisabledCodes(uint status, bool expected)
    {
        Assert.Equal(expected, SystemRestoreService.IsProtectionDisabledStatus(status));
    }

    [Fact]
    public void IsWithinCreationThrottle_ThroughTheDouble_SeesTheReportedPointsAndSetting()
    {
        var service = new FakeSystemRestoreService
        {
            FrequencyMinutes = 1440,
            Points = [new SystemRestoreService.RestorePointInfo(7, 12, "recent", Now.AddHours(-1))],
        };

        Assert.True(service.IsWithinCreationThrottle(Now));
        Assert.Equal(Now.AddHours(-1), service.GetNewestRestorePointUtc());
    }

    [Fact]
    public void IsWithinCreationThrottle_FrequencyZeroThroughTheDouble_IsNotThrottled()
    {
        var service = new FakeSystemRestoreService
        {
            FrequencyMinutes = 0,
            Points = [new SystemRestoreService.RestorePointInfo(7, 12, "just now", Now.AddSeconds(-30))],
        };

        Assert.False(service.IsWithinCreationThrottle(Now));
    }

    [Fact]
    public void IsWithinCreationThrottle_NoReadablePoints_FailsOpen()
    {
        var service = new FakeSystemRestoreService { FrequencyMinutes = 1440, Points = [] };

        Assert.False(service.IsWithinCreationThrottle(Now));
    }

    [Fact]
    public void CreateRestorePoint_SurfacesTheNativeStatusUnchanged()
    {
        var service = new FakeSystemRestoreService
        {
            CreateResult = new SystemRestoreService.SystemRestoreResult(
                false,
                SystemRestoreService.ErrorServiceDisabled
            ),
        };

        var result = service.CreateRestorePoint("optimizerDuck");

        Assert.False(result.Succeeded);
        Assert.Equal(SystemRestoreService.ErrorServiceDisabled, result.NativeStatus);
        Assert.True(SystemRestoreService.IsProtectionDisabledStatus(result.NativeStatus));
    }

    [Fact]
    public void EnableProtection_PassesTheDriveThrough()
    {
        var service = new FakeSystemRestoreService();

        var result = service.EnableProtection("C:");

        Assert.True(result.Succeeded);
        Assert.Equal("C:", service.EnabledDrive);
    }
}
