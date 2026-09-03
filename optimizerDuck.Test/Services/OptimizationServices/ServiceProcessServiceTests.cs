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
        var (result, notFound) = await ServiceProcessService.GetStartupTypeAsync("msiserver");

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

    // =============================================
    // Integration tests: ChangeServiceStartupTypeAsync (real sc.exe)
    // =============================================

    private const string TestServiceName = "odTestSvc";

    private static bool IsElevated()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(identity).IsInRole(
            System.Security.Principal.WindowsBuiltInRole.Administrator
        );
    }

    private static async Task EnsureTestServiceAsync()
    {
        var (startType, notFound) = await ServiceProcessService.GetStartupTypeAsync(
            TestServiceName
        );
        if (notFound)
        {
            var (exitCode, _) = await RunScAsync(
                $"create {TestServiceName} binPath= \"C:\\Windows\\System32\\svchost.exe -k test\" start= demand"
            );
            Assert.Equal(0, exitCode);
        }
        else if (startType != ServiceStartupType.Manual)
        {
            var (exitCode, _) = await RunScAsync($"config {TestServiceName} start= demand");
            Assert.Equal(0, exitCode);
        }
    }

    private static async Task<(int ExitCode, string Stdout)> RunScAsync(string arguments)
    {
        using var process = System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        );
        Assert.NotNull(process);

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill();
            }
            catch { }
            Assert.Fail($"sc.exe timed out after 30s: {arguments}");
        }

        var stdout = await stdoutTask;
        await stderrTask;
        return (process.ExitCode, stdout);
    }

    private static async Task DeleteTestServiceBestEffortAsync()
    {
        try
        {
            await RunScAsync($"delete {TestServiceName}");
        }
        catch
        {
            // Cleanup must never mask the test assertion.
        }
    }

    private static async Task StripChangeConfigFromAdminsAsync(string serviceName)
    {
        var (_, output) = await RunScAsync($"sdshow {serviceName}");
        var sddl = System
            .Text.RegularExpressions.Regex.Match(
                output,
                @"D:\(.+\)",
                System.Text.RegularExpressions.RegexOptions.Compiled
            )
            .Value;
        Assert.StartsWith("D:", sddl);

        // Remove DC (SERVICE_CHANGE_CONFIG) from the BA (BUILTIN\Administrators) ACE.
        var patched = System.Text.RegularExpressions.Regex.Replace(
            sddl,
            @"\(A;;[A-Z]*DC[A-Z]*;;;BA\)",
            m => m.Value.Replace("DC", string.Empty),
            System.Text.RegularExpressions.RegexOptions.Compiled
        );
        Assert.NotEqual(sddl, patched);

        var (_, set) = await RunScAsync($"sdset {serviceName} {patched}");
        Assert.Contains("SUCCESS", set, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChangeServiceStartupTypeAsync_AlreadyConfigured_SkipsChange_ReturnsAlreadyConfigured()
    {
        if (!IsElevated())
            Assert.Skip("Service creation requires an elevated test host.");
        await EnsureTestServiceAsync();
        try
        {
            var (startType, _) = await ServiceProcessService.GetStartupTypeAsync(TestServiceName);
            Assert.NotNull(startType);

            var result = await ServiceProcessService.ChangeServiceStartupTypeAsync(
                new ServiceItem(TestServiceName, startType.Value)
            );

            Assert.Equal(ServiceChangeResult.AlreadyConfigured, result);
        }
        finally
        {
            await DeleteTestServiceBestEffortAsync();
        }
    }

    [Fact]
    public async Task ChangeServiceStartupTypeAsync_Success_ChangesType_ReturnsSuccess()
    {
        if (!IsElevated())
            Assert.Skip("Service creation requires an elevated test host.");
        await EnsureTestServiceAsync();
        try
        {
            var result = await ServiceProcessService.ChangeServiceStartupTypeAsync(
                new ServiceItem(TestServiceName, ServiceStartupType.Disabled)
            );

            Assert.Equal(ServiceChangeResult.Success, result);
            var (startType, _) = await ServiceProcessService.GetStartupTypeAsync(TestServiceName);
            Assert.Equal(ServiceStartupType.Disabled, startType);
        }
        finally
        {
            await DeleteTestServiceBestEffortAsync();
        }
    }

    [Fact]
    public async Task ChangeServiceStartupTypeAsync_NonexistentService_ReturnsNotFound()
    {
        var result = await ServiceProcessService.ChangeServiceStartupTypeAsync(
            new ServiceItem(
                "OptimizerDuckTest_Nonexistent_Service_12345",
                ServiceStartupType.Manual
            )
        );

        Assert.Equal(ServiceChangeResult.NotFound, result);
    }

    [Fact]
    public async Task ChangeServiceStartupTypeAsync_ProtectedServiceDacl_ReturnsAccessDenied()
    {
        if (!IsElevated())
            Assert.Skip("Service creation requires an elevated test host.");
        await EnsureTestServiceAsync();
        try
        {
            await StripChangeConfigFromAdminsAsync(TestServiceName);

            var result = await ServiceProcessService.ChangeServiceStartupTypeAsync(
                new ServiceItem(TestServiceName, ServiceStartupType.Disabled)
            );

            Assert.Equal(ServiceChangeResult.AccessDenied, result);
        }
        finally
        {
            await DeleteTestServiceBestEffortAsync();
        }
    }
}
