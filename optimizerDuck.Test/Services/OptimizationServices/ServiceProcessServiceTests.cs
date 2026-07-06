using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Services.Optimization.Providers;

namespace optimizerDuck.Test.Services.OptimizationServices;

public class ServiceProcessServiceTests
{
    // =============================================
    // Unit tests: ParseScStartType (hardcoded input)
    // =============================================

    [Fact]
    public void ParseScStartType_AutoStart_ReturnsAutomatic()
    {
        var stdout = """
            SERVICE_NAME: Audiosrv
                    TYPE               : 20  WIN32_SHARE_PROCESS
                    START_TYPE         : 2   AUTO_START
                    ERROR_CONTROL      : 1   NORMAL
            """;

        var (result, parseFailed) = ServiceProcessService.ParseScStartType(stdout);

        Assert.False(parseFailed);
        Assert.Equal(ServiceStartupType.Automatic, result);
    }

    [Fact]
    public void ParseScStartType_AutoStartDelayed_ReturnsAutomaticDelayedStart()
    {
        var stdout = """
            SERVICE_NAME: CDPSvc
                    TYPE               : 20  WIN32_SHARE_PROCESS
                    START_TYPE         : 2   AUTO_START  (DELAYED)
                    ERROR_CONTROL      : 1   NORMAL
            """;

        var (result, parseFailed) = ServiceProcessService.ParseScStartType(stdout);

        Assert.False(parseFailed);
        Assert.Equal(ServiceStartupType.AutomaticDelayedStart, result);
    }

    [Fact]
    public void ParseScStartType_DemandStart_ReturnsManual()
    {
        var stdout = """
            SERVICE_NAME: BITS
                    TYPE               : 20  WIN32_SHARE_PROCESS
                    START_TYPE         : 3   DEMAND_START
                    ERROR_CONTROL      : 1   NORMAL
            """;

        var (result, parseFailed) = ServiceProcessService.ParseScStartType(stdout);

        Assert.False(parseFailed);
        Assert.Equal(ServiceStartupType.Manual, result);
    }

    [Fact]
    public void ParseScStartType_Disabled_ReturnsDisabled()
    {
        var stdout = """
            SERVICE_NAME: AppVClient
                    TYPE               : 10  WIN32_OWN_PROCESS
                    START_TYPE         : 4   DISABLED
                    ERROR_CONTROL      : 1   NORMAL
            """;

        var (result, parseFailed) = ServiceProcessService.ParseScStartType(stdout);

        Assert.False(parseFailed);
        Assert.Equal(ServiceStartupType.Disabled, result);
    }

    [Fact]
    public void ParseScStartType_BootStart_ReturnsNullNotFailed()
    {
        var stdout = """
            SERVICE_NAME: ACPI
                    TYPE               : 1  KERNEL_DRIVER
                    START_TYPE         : 0   BOOT_START
                    ERROR_CONTROL      : 1   NORMAL
            """;

        var (result, parseFailed) = ServiceProcessService.ParseScStartType(stdout);

        Assert.False(parseFailed);
        Assert.Null(result);
    }

    [Fact]
    public void ParseScStartType_SystemStart_ReturnsNullNotFailed()
    {
        var stdout = """
            SERVICE_NAME: AFD
                    TYPE               : 1  KERNEL_DRIVER
                    START_TYPE         : 1   SYSTEM_START
                    ERROR_CONTROL      : 1   NORMAL
            """;

        var (result, parseFailed) = ServiceProcessService.ParseScStartType(stdout);

        Assert.False(parseFailed);
        Assert.Null(result);
    }

    [Fact]
    public void ParseScStartType_NoStartTypeLine_ReturnsParseFailed()
    {
        var stdout = """
            SERVICE_NAME: TestSvc
                    BINARY_PATH_NAME   : C:\test.exe
            """;

        var (result, parseFailed) = ServiceProcessService.ParseScStartType(stdout);

        Assert.True(parseFailed);
        Assert.Null(result);
    }

    [Fact]
    public void ParseScStartType_IgnoresErrorControlLine_ReturnsCorrectType()
    {
        var stdout = """
            SERVICE_NAME: Test
                    TYPE               : 20  WIN32_SHARE_PROCESS
                    START_TYPE         : 3   DEMAND_START
                    ERROR_CONTROL      : 1   NORMAL
            """;

        var (result, parseFailed) = ServiceProcessService.ParseScStartType(stdout);

        Assert.False(parseFailed);
        Assert.Equal(ServiceStartupType.Manual, result);
    }

    [Fact]
    public void ParseScStartType_IgnoresTagZero_ReturnsCorrectType()
    {
        var stdout = """
            SERVICE_NAME: Test
                    TYPE               : 20  WIN32_SHARE_PROCESS
                    START_TYPE         : 3   DEMAND_START
                    ERROR_CONTROL      : 1   NORMAL
                    TAG                : 0
            """;

        var (result, parseFailed) = ServiceProcessService.ParseScStartType(stdout);

        Assert.False(parseFailed);
        Assert.Equal(ServiceStartupType.Manual, result);
    }

    [Fact]
    public void ParseScStartType_LocaleIndependentFieldName_ReturnsCorrectType()
    {
        var stdout = """
            SERVICE_NAME: Test
                    FOO_TYPE           : 20  WIN32_SHARE_PROCESS
                    STARTTYP           : 2   AUTOMATISCHER_START
                    FEHLERKONTROLLE    : 1   NORMAL
            """;

        var (result, parseFailed) = ServiceProcessService.ParseScStartType(stdout);

        Assert.False(parseFailed);
        Assert.Equal(ServiceStartupType.Automatic, result);
    }

    [Fact]
    public void ParseScStartType_LocaleIndependentDelayed_ReturnsAutomaticDelayedStart()
    {
        var stdout = """
            SERVICE_NAME: Test
                    STARTTYP           : 2   AUTOMATISCHER_START (VERZÖGERT)
            """;

        var (result, parseFailed) = ServiceProcessService.ParseScStartType(stdout);

        Assert.False(parseFailed);
        Assert.Equal(ServiceStartupType.AutomaticDelayedStart, result);
    }

    [Fact]
    public void ParseScStartType_EmptyOutput_ReturnsParseFailed()
    {
        var (result, parseFailed) = ServiceProcessService.ParseScStartType("");

        Assert.True(parseFailed);
        Assert.Null(result);
    }

    // =============================================
    // Integration tests: GetStartupTypeAsync (real sc.exe)
    // =============================================

    [Fact]
    public async Task GetStartupTypeAsync_ExistingAutoService_ReturnsAutomatic()
    {
        var (result, notFound) = await ServiceProcessService.GetStartupTypeAsync("Audiosrv");

        Assert.False(notFound);
        Assert.NotNull(result);
        Assert.Equal(ServiceStartupType.Automatic, result);
    }

    [Fact]
    public async Task GetStartupTypeAsync_ExistingDemandService_ReturnsManual()
    {
        var (result, notFound) = await ServiceProcessService.GetStartupTypeAsync("BITS");

        Assert.False(notFound);
        Assert.NotNull(result);
        Assert.Equal(ServiceStartupType.Manual, result);
    }

    [Fact]
    public async Task GetStartupTypeAsync_NonexistentService_ReturnsNotFound()
    {
        var (_, notFound) = await ServiceProcessService.GetStartupTypeAsync(
            "OptimizerDuckTest_Nonexistent_Service_12345"
        );

        Assert.True(notFound);
    }
}
