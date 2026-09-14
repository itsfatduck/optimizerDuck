using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Test.Services.OptimizationServices;

public class ServiceProcessServiceTests
{
    private static OpCall NewCall() =>
        new() { Changes = new ChangeSet(), Logger = NullLogger.Instance };

    [Theory]
    [InlineData(true, 5, "ChangeServiceConfig2")]
    [InlineData(false, 5, "ChangeServiceConfig")]
    public void BuildWriteErrorDetail_NamesTheCallThatFailed(
        bool startTypeWritten,
        int nativeError,
        string expectedCall
    )
    {
        // A refusal after the start type was written must not read like "nothing ran".
        var detail = ServiceProcessService.BuildWriteErrorDetail(startTypeWritten, nativeError);

        Assert.Contains(expectedCall, detail);
        Assert.Contains(nativeError.ToString(), detail);
    }

    [Fact]
    public void BuildWriteErrorDetail_WithAThrowAfterTheWrite_NamesTheThrow()
    {
        // The interop itself threw after the start type was written: the throw text is the only
        // honest reason, so it is reported instead of the error code.
        var detail = ServiceProcessService.BuildWriteErrorDetail(
            true,
            5,
            "LoadLibrary failed"
        );

        Assert.Contains("LoadLibrary failed", detail);
        Assert.Contains("after the start type was written", detail);
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
    // Contract fixture: read mapping against registry ground truth.
    // Independent of the mechanism: the expected value comes from
    // HKLM\SYSTEM\CurrentControlSet\Services, where Windows stores Start (2 auto / 3 manual /
    // 4 disabled) and the DelayedAutoStart flag. Driver entries are included on purpose - the
    // Service Control Manager reports their start type without the TYPE/START_TYPE ambiguity that
    // sc.exe output had. Boot (0) and system (1) starts are excluded: they are outside the app's
    // ServiceStartupType domain, and the registry is not authoritative for them (55 uninstalled
    // driver entries on this machine report DEMAND_START through the SCM, and sc.exe - which also
    // reads the SCM - reported exactly the same before this conversion).
    // =============================================

    private const int PerTypeSampleLimit = 8;

    /// <summary>
    ///     Registry truth for one services entry: the startup type implied by Start plus the
    ///     DelayedAutoStart flag, and whether this is a Win32 service (Type 0x10 | 0x20 | 0x30)
    ///     rather than a driver (Type 1 kernel, 2 file system, 4 recognizer, 8 adapter).
    /// </summary>
    private static (
        bool Found,
        ServiceStartupType? Expected,
        bool IsWin32Service
    ) ReadRegistryTruth(RegistryKey services, string name)
    {
        using var key = services.OpenSubKey(name);
        if (
            key?.GetValue("ImagePath") is null
            || key.GetValue("Start") is not int start
            || key.GetValue("Type") is not int type
        )
            return (false, null, false);

        var delayed = key.GetValue("DelayedAutoStart") is int flag && flag == 1;
        ServiceStartupType? expected = start switch
        {
            2 => delayed ? ServiceStartupType.AutomaticDelayedStart : ServiceStartupType.Automatic,
            3 => ServiceStartupType.Manual,
            4 => ServiceStartupType.Disabled,
            _ => null,
        };

        return (true, expected, (type & 0x10) != 0);
    }

    [Fact]
    public async Task GetStartupTypeAsync_MatchesRegistryGroundTruth_SampledAcrossTypes()
    {
        using var services = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
        Assert.NotNull(services);

        // Nullable value types cannot be Dictionary keys, so buckets are a list of pairs.
        var buckets = new List<(ServiceStartupType? Expected, List<string> Names)>
        {
            (ServiceStartupType.Automatic, []),
            (ServiceStartupType.AutomaticDelayedStart, []),
            (ServiceStartupType.Manual, []),
            (ServiceStartupType.Disabled, []),
        };

        foreach (var name in services!.GetSubKeyNames())
        {
            var (found, expected, _) = ReadRegistryTruth(services, name);
            if (!found || expected is null)
                continue;

            var bucket = buckets.First(b => b.Expected == expected).Names;
            if (bucket.Count < PerTypeSampleLimit)
                bucket.Add(name);
        }

        var checkedNames = 0;
        foreach (var (expected, names) in buckets)
        {
            foreach (var name in names)
            {
                var (actual, notFound) = await ServiceProcessService.GetStartupTypeAsync(name);

                Assert.False(
                    notFound,
                    $"'{name}' has an ImagePath in the registry but was reported as not found"
                );
                Assert.Equal(expected, actual);
                checkedNames++;
            }
        }

        Assert.True(checkedNames > 0, "no service could be sampled from the registry");
        Assert.True(
            buckets.Count(b => b.Expected is not null && b.Names.Count > 0) >= 2,
            $"expected at least two startup types on this machine, sampled {checkedNames} services"
        );
    }

    // =============================================
    // Integration tests: ChangeServiceStartupTypeAsync (real service, SCM writes).
    // sc.exe stays in this file only to create/inspect the scratch test service.
    // =============================================

    private const string TestServiceName = "odTestSvc";

    private static bool IsElevated()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(identity).IsInRole(
            System.Security.Principal.WindowsBuiltInRole.Administrator
        );
    }

    private async Task EnsureTestServiceAsync()
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
                NewCall(),
                new ServiceItem(TestServiceName, startType.Value)
            );

            // AlreadyConfigured maps to informational success (Ok).
            Assert.True(result.Ok, result.Error);
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
                NewCall(),
                new ServiceItem(TestServiceName, ServiceStartupType.Disabled)
            );

            Assert.True(result.Ok, result.Error);
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
        var call = NewCall();
        var result = await ServiceProcessService.ChangeServiceStartupTypeAsync(
            call,
            new ServiceItem(
                "OptimizerDuckTest_Nonexistent_Service_12345",
                ServiceStartupType.Manual
            )
        );

        // NotFound maps to informational success (Ok), and there is nothing to revert.
        Assert.True(result.Ok, result.Error);
        Assert.Null(Assert.Single(call.Changes.Changes).Revert);
    }

    [Fact]
    public async Task ChangeServiceStartupTypeAsync_AllFourTypes_RoundTripThroughRead()
    {
        if (!IsElevated())
            Assert.Skip("Service creation requires an elevated test host.");
        await EnsureTestServiceAsync();
        try
        {
            // Automatic runs first because the scratch service is created without a delayed
            // flag; whether sc.exe (or a later SCM write) clears that flag when moving back to
            // plain automatic is a real behaviour worth pinning separately, and this order
            // does not depend on the answer.
            var previousType = ServiceStartupType.Manual;

            foreach (
                var target in new[]
                {
                    ServiceStartupType.Automatic,
                    ServiceStartupType.Manual,
                    ServiceStartupType.Disabled,
                    ServiceStartupType.AutomaticDelayedStart,
                }
            )
            {
                var call = NewCall();
                var result = await ServiceProcessService.ChangeServiceStartupTypeAsync(
                    call,
                    new ServiceItem(TestServiceName, target)
                );
                Assert.True(result.Ok, $"{target}: {result.Error}");

                // The recorded step must carry the type Windows reported before the write, or a
                // revert cannot restore it. The scratch service starts as demand (Manual).
                var revert = Assert.IsType<ServiceRevertStep>(
                    Assert.Single(call.Changes.Changes).Revert
                );
                Assert.Equal(previousType, revert.OriginalStartupType);
                previousType = target;

                var (readBack, notFound) = await ServiceProcessService.GetStartupTypeAsync(
                    TestServiceName
                );
                Assert.False(notFound, $"{target}: service reported as not found after the write");
                Assert.Equal(target, readBack);
            }
        }
        finally
        {
            await DeleteTestServiceBestEffortAsync();
        }
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

            var call = NewCall();
            var result = await ServiceProcessService.ChangeServiceStartupTypeAsync(
                call,
                new ServiceItem(TestServiceName, ServiceStartupType.Disabled)
            );

            // Windows protects this service. That is a refusal, not a failure: the call reports
            // success for the batch, records no revert step, and the step says Windows refused
            // rather than that the machine already matched.
            Assert.True(result.Ok, result.Error);
            var change = Assert.Single(call.Changes.Changes);
            Assert.Equal(ChangeKind.Refused, change.Kind);
            Assert.Null(change.Revert);
        }
        finally
        {
            await DeleteTestServiceBestEffortAsync();
        }
    }
}
