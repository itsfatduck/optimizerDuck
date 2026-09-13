using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Power;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Test.Services;

/// <summary>
/// Unit tests for the lean power core's edge recording and atomic install.
/// Hand-subclassed doubles (no mocking libraries); no native calls.
/// </summary>
public class PowerPlanChangesTests
{
    private sealed class StubPowerPlanService : PowerPlanService
    {
        public StubPowerPlanService()
            : base(NullLogger<PowerPlanService>.Instance) { }

        public Guid? ActiveId { get; set; } = Guid.NewGuid();

        public int ActivateCalls { get; private set; }

        public Func<Guid, ActivationResult>? ActivateFunc { get; set; }

        public Func<string, Guid, Task<SchemeRefResult>>? ImportFunc { get; set; }

        public Func<string, Guid, Task<InstallResult>>? InstallFunc { get; set; }

        public Func<Guid, Guid, Guid, uint?, uint?, SettingWriteResult>? WriteFunc { get; set; }

        public override Guid? GetActiveSchemeId() => ActiveId;

        public override ActivationResult SetActiveScheme(Guid schemeId, ILogger? logger = null)
        {
            ActivateCalls++;
            return ActivateFunc?.Invoke(schemeId)
                ?? new ActivationResult(OpResult.Fail("no stub", "no stub"), null);
        }

        public override Task<SchemeRefResult> ImportSchemeAsync(
            string filePath,
            Guid destinationId,
            ILogger? logger = null,
            CancellationToken ct = default
        ) =>
            ImportFunc is null
                ? Task.FromResult(new SchemeRefResult(OpResult.Fail("no stub", "no stub"), null))
                : ImportFunc(filePath, destinationId);

        public override Task<InstallResult> InstallSchemeAsync(
            string filePath,
            Guid destinationId,
            ILogger? logger = null,
            CancellationToken ct = default
        ) =>
            InstallFunc is null
                ? base.InstallSchemeAsync(filePath, destinationId, logger, ct)
                : InstallFunc(filePath, destinationId);

        public override SettingWriteResult SetSetting(
            Guid schemeId,
            Guid subgroupId,
            Guid settingId,
            uint? acValue,
            uint? dcValue,
            ILogger? logger = null
        ) =>
            WriteFunc?.Invoke(schemeId, subgroupId, settingId, acValue, dcValue)
            ?? new SettingWriteResult(OpResult.Fail("no stub", "no stub"), null, null);
    }

    private static OpCall NewCall() => new() { Logger = NullLogger.Instance };

    [Fact]
    public void Activate_Success_RecordsStepWithPreviousAndInstalled()
    {
        var previous = Guid.NewGuid();
        var target = Guid.NewGuid();
        var installed = Guid.NewGuid();
        var plans = new StubPowerPlanService
        {
            ActivateFunc = _ => new ActivationResult(OpResult.Success(), previous),
        };
        var call = NewCall();

        var result = PowerPlanChanges.Activate(call, plans, target, installed);

        Assert.True(result.Ok);
        var change = Assert.Single(call.Changes.Changes);
        Assert.True(change.Ok);
        var step = Assert.IsType<PowerPlanRevertStep>(change.Revert);
        Assert.Equal(previous, step.PreviousSchemeId);
        Assert.Equal(installed, step.InstalledSchemeId);
    }

    [Fact]
    public async Task Activate_Failure_RecordsErrorWithRetry()
    {
        var plans = new StubPowerPlanService
        {
            ActivateFunc = _ => new ActivationResult(
                OpResult.Fail("PowerSetActiveScheme failed.", "detail"),
                null
            ),
        };
        var call = NewCall();

        var result = PowerPlanChanges.Activate(call, plans, Guid.NewGuid());

        Assert.False(result.Ok);
        var change = Assert.Single(call.Changes.FailedSteps);
        Assert.NotNull(change.Retry);
        var retryCall = NewCall();
        var retryResult = await change.Retry(retryCall);
        Assert.False(retryResult.Ok);
        Assert.Single(retryCall.Changes.FailedSteps);
    }

    [Fact]
    public void Activate_AlreadyActive_RecordsSkippedWithoutStep()
    {
        var active = Guid.NewGuid();
        var plans = new StubPowerPlanService
        {
            ActivateFunc = _ => new ActivationResult(OpResult.Success(), active),
        };
        var call = NewCall();

        var result = PowerPlanChanges.Activate(call, plans, active);

        Assert.True(result.Ok);
        var change = Assert.Single(call.Changes.SuccessfulSteps);
        Assert.Null(change.Revert);
    }

    [Fact]
    public void WriteSetting_Success_RecordsPreviousValues()
    {
        var plans = new StubPowerPlanService
        {
            WriteFunc = (_, _, _, _, _) => new SettingWriteResult(OpResult.Success(), 10u, 20u),
        };
        var call = NewCall();
        var scheme = Guid.NewGuid();
        var group = Guid.NewGuid();
        var setting = Guid.NewGuid();

        var result = PowerPlanChanges.WriteSetting(call, plans, scheme, group, setting, 1u, 2u);

        Assert.True(result.Ok);
        var step = Assert.IsType<PowerSettingRevertStep>(
            Assert.Single(call.Changes.SuccessfulSteps).Revert
        );
        Assert.Equal(10u, step.PreviousAcValue);
        Assert.Equal(20u, step.PreviousDcValue);
    }

    [Fact]
    public void WriteSetting_Failure_RecordsErrorWithRetry()
    {
        var plans = new StubPowerPlanService
        {
            WriteFunc = (_, _, _, _, _) =>
                new SettingWriteResult(OpResult.Fail("nope", "detail"), null, null),
        };
        var call = NewCall();

        var result = PowerPlanChanges.WriteSetting(
            call,
            plans,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1u,
            null
        );

        Assert.False(result.Ok);
        Assert.NotNull(Assert.Single(call.Changes.FailedSteps).Retry);
    }

    [Fact]
    public async Task InstallAsync_Success_RecordsSingleStep()
    {
        var installed = Guid.NewGuid();
        var previous = Guid.NewGuid();
        var plans = new StubPowerPlanService
        {
            InstallFunc = (_, _) =>
                Task.FromResult(new InstallResult(OpResult.Success(), installed, previous)),
        };
        var call = NewCall();

        var install = await PowerPlanChanges.InstallAsync(
            call,
            plans,
            "x.pow",
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );
        var result = install.Result;
        var installedId = install.InstalledId;
        var previousId = install.PreviousId;

        Assert.True(result.Ok);
        Assert.Equal(installed, installedId);
        Assert.Equal(previous, previousId);
        var step = Assert.IsType<PowerPlanRevertStep>(
            Assert.Single(call.Changes.SuccessfulSteps).Revert
        );
        Assert.Equal(previous, step.PreviousSchemeId);
        Assert.Equal(installed, step.InstalledSchemeId);
    }

    [Fact]
    public async Task InstallAsync_ImportFailure_KeepsPreviousForErrorMapping()
    {
        var previous = Guid.NewGuid();
        var plans = new StubPowerPlanService
        {
            InstallFunc = (_, _) =>
                Task.FromResult(
                    new InstallResult(
                        OpResult.Fail("PowerImportPowerScheme failed.", "detail"),
                        null,
                        previous
                    )
                ),
        };
        var call = NewCall();

        var install = await PowerPlanChanges.InstallAsync(
            call,
            plans,
            "missing.pow",
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );
        var result = install.Result;
        var installedId = install.InstalledId;
        var previousId = install.PreviousId;

        Assert.False(result.Ok);
        Assert.Null(installedId);
        Assert.Equal(previous, previousId);
        Assert.Single(call.Changes.FailedSteps);
    }

    [Fact]
    public async Task InstallScheme_ImportFails_DoesNotActivate()
    {
        var previous = Guid.NewGuid();
        var plans = new StubPowerPlanService
        {
            ActiveId = previous,
            ImportFunc = (_, _) =>
                Task.FromResult(
                    new SchemeRefResult(
                        OpResult.Fail("PowerImportPowerScheme failed.", "detail"),
                        null
                    )
                ),
            ActivateFunc = _ => new ActivationResult(OpResult.Success(), previous),
        };

        var install = await plans.InstallSchemeAsync(
            "missing.pow",
            Guid.NewGuid(),
            ct: TestContext.Current.CancellationToken
        );
        var result = install.Result;
        var installedId = install.InstalledId;
        var previousId = install.PreviousId;

        Assert.False(result.Ok);
        Assert.Null(installedId);
        Assert.Equal(previous, previousId);
        Assert.Equal(0, plans.ActivateCalls);
    }

    [Fact]
    public async Task InstallScheme_ActivateFails_ReturnsInstalledAndPrevious()
    {
        var previous = Guid.NewGuid();
        var installed = Guid.NewGuid();
        var plans = new StubPowerPlanService
        {
            ActiveId = previous,
            ImportFunc = (_, _) =>
                Task.FromResult(new SchemeRefResult(OpResult.Success(), installed)),
            ActivateFunc = _ => new ActivationResult(
                OpResult.Fail("PowerSetActiveScheme failed.", "detail"),
                null
            ),
        };

        var install = await plans.InstallSchemeAsync(
            "x.pow",
            Guid.NewGuid(),
            ct: TestContext.Current.CancellationToken
        );
        var result = install.Result;
        var installedId = install.InstalledId;
        var previousId = install.PreviousId;

        Assert.False(result.Ok);
        Assert.Equal(installed, installedId);
        Assert.Equal(previous, previousId);
    }

    [Fact]
    public async Task InstallScheme_Success_ReturnsBothIds()
    {
        var previous = Guid.NewGuid();
        var installed = Guid.NewGuid();
        var plans = new StubPowerPlanService
        {
            ActiveId = previous,
            ImportFunc = (_, _) =>
                Task.FromResult(new SchemeRefResult(OpResult.Success(), installed)),
            ActivateFunc = _ => new ActivationResult(OpResult.Success(), previous),
        };

        var install = await plans.InstallSchemeAsync(
            "x.pow",
            installed,
            ct: TestContext.Current.CancellationToken
        );
        var result = install.Result;
        var installedId = install.InstalledId;
        var previousId = install.PreviousId;

        Assert.True(result.Ok);
        Assert.Equal(installed, installedId);
        Assert.Equal(previous, previousId);
        Assert.Equal(1, plans.ActivateCalls);
    }

    [Fact]
    public async Task ImportSchemeAsync_MissingFile_FailsWithoutNativeCall()
    {
        var service = new PowerPlanService(NullLogger<PowerPlanService>.Instance);

        var import = await service.ImportSchemeAsync(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pow"),
            Guid.NewGuid(),
            ct: TestContext.Current.CancellationToken
        );
        var result = import.Result;
        var schemeId = import.SchemeId;

        Assert.False(result.Ok);
        Assert.Null(schemeId);
        Assert.Contains("PowerImportPowerScheme", result.Error);
    }
}
