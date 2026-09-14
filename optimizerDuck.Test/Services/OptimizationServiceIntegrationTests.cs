using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.Revert;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.History;
using optimizerDuck.Services.Optimization;
using optimizerDuck.Services.Revert;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.Test.TestDoubles;
using Xunit;

namespace optimizerDuck.Test.Services;

public class OptimizationServiceIntegrationTests : IDisposable
{
    private class PartialFailureOptimization : StubOptimization
    {
        private int _stepCount;
        private readonly bool _shouldFail;

        public override string OptimizationKey => "PartialFailureTest";
        public override string Name => "Partial Failure Test";
        public override string ShortDescription => "Tests partial failure scenarios";

        public PartialFailureOptimization(bool shouldFail = false)
        {
            _shouldFail = shouldFail;
        }

        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            // Step 1: Always succeeds
            context.Changes.Add("Step 1", "Step 1", true, new TestRevertStep { StepId = 1 });
            _stepCount++;

            // Step 2: May fail
            if (_shouldFail)
            {
                context.Changes.Add("Step 2", "Step 2", false, error: "Simulated failure");
                _stepCount++;
            }

            // Step 3: Only executes if step 2 succeeded
            if (!_shouldFail)
            {
                context.Changes.Add("Step 3", "Step 3", true, new TestRevertStep { StepId = 3 });
                _stepCount++;
            }

            var success = !_shouldFail;
            return Task.FromResult(
                success ? ApplyResult.True() : ApplyResult.False("Step 2 failed intentionally")
            );
        }
    }

    private class TestRevertStep : IRevertStep
    {
        public int StepId { get; init; }
        public string Type => "Test";
        public string Description => $"Revert step {StepId}";

        public Task<bool> ExecuteAsync(RevertContext _, ILogger logger)
        {
            // Simulate revert operation
            return Task.FromResult(true);
        }

        public JObject ToData()
        {
            return new JObject { ["StepId"] = StepId };
        }
    }

    private class NothingToDoOptimization : StubOptimization
    {
        public override string OptimizationKey => "NothingToDoReportTest";
        public override string Name => "Nothing to do report test";
        public override string ShortDescription => "Records only a skip";

        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            context.Changes.AddSkip("Service", "Service 'x' is already configured");
            return Task.FromResult(context.Changes.ToApplyResult());
        }
    }

    private class RegistryStepOptimization : StubOptimization
    {
        public string KeyPath { get; init; } = string.Empty;

        public override string OptimizationKey => "RegistryStepReportTest";
        public override string Name => "Registry step report test";
        public override string ShortDescription => "Writes one registry value";

        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            // A real provider, so the recorded step is one the revert manager can actually run.
            RegistryService.Write(context, new RegistryItem(KeyPath, "Reported", 1));
            return Task.FromResult(context.Changes.ToApplyResult());
        }
    }

    private readonly List<Guid> _testOptimizationIds = new();

    public OptimizationServiceIntegrationTests() { }

    public void Dispose()
    {
        // Clean up only test files created by this class
        foreach (var id in _testOptimizationIds)
        {
            foreach (var path in new[]
            {
                Path.Combine(Shared.RevertDirectory, id + ".json"),
                ChangeRecordStore.PathFor(id),
                ChangeRecordStore.PathFor(id) + ".tmp",
            })
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch
                {
                    // Ignore
                }
            }
        }
    }

    [Fact]
    public async Task ApplyAsync_WithSuccess_SavesRevertDataCorrectly()
    {
        var loggerFactory = NullLoggerFactory.Instance;
        var revertManager = new RevertManager(
            NullLogger<RevertManager>.Instance,
            TestShell.New(),
            new PowerPlanService(NullLogger<PowerPlanService>.Instance),
            TimeProvider.System
        );
        var systemInfoService = new SystemInfoService(NullLogger<SystemInfoService>.Instance);
        var streamService = new StreamService(NullLogger<StreamService>.Instance);

        var optimizationService = new OptimizationService(
            revertManager,
            loggerFactory,
            systemInfoService,
            streamService,
            null!,
            new ShellService(new ProcessRunner(120000)),
            new PowerPlanService(NullLogger<PowerPlanService>.Instance),
            new SystemRestoreService(NullLogger<SystemRestoreService>.Instance),
            NullLogger<OptimizationService>.Instance
        );

        var optimization = new PartialFailureOptimization(shouldFail: false);
        _testOptimizationIds.Add(optimization.Id);
        var progress = new Progress<ProcessingProgress>();

        var result = await optimizationService.ApplyAsync(
            optimization,
            progress,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(OptimizationSuccessResult.Success, result.Status);

        // Verify revert data was saved
        var revertData = await RevertManager.GetRevertDataAsync(optimization.Id);
        Assert.NotNull(revertData);
        Assert.Equal(2, revertData.Steps.Count(s => s != null)); // 2 successful steps
    }

    [Fact]
    public async Task UpdateOptimizationStateAsync_WithMissingData_HandlesGracefully()
    {
        var optimizations = new IOptimization[]
        {
            new PartialFailureOptimization(shouldFail: false),
            new PartialFailureOptimization(shouldFail: true),
        };

        // These optimizations have never been applied, so no revert data exists
        await OptimizationService.UpdateOptimizationStateAsync(optimizations);

        // All should be marked as not applied
        Assert.False(optimizations[0].State.IsApplied);
        Assert.False(optimizations[1].State.IsApplied);
    }

    [Fact]
    public async Task RetryFailedStepsAsync_WithRetryableSteps_Succeeds()
    {
        var failedSteps = new List<Change>
        {
            new()
            {
                Index = 1,
                Name = "Test",
                Description = "Failed step",
                Ok = false,
                Error = "Temporary failure",
                Retry = _ => Task.FromResult(OpResult.Success()),
            },
        };

        var logger = NullLogger.Instance;
        var recoveredSteps = await OptimizationService.RetryFailedStepsAsync(
            failedSteps,
            false,
            logger,
            null
        );

        // Should succeed on retry
        Assert.Empty(recoveredSteps);
    }

    [Fact]
    public async Task RetryFailedStepsAsync_WithNonRetryableSteps_RemainsFailed()
    {
        var failedSteps = new List<Change>
        {
            new()
            {
                Index = 1,
                Name = "Test",
                Description = "Failed step",
                Ok = false,
                Error = "Permanent failure",
                Retry = null, // No retry action
            },
        };

        var logger = NullLogger.Instance;
        var remainingFailed = await OptimizationService.RetryFailedStepsAsync(
            failedSteps,
            false,
            logger,
            null
        );

        // Should remain failed
        Assert.Single(remainingFailed);
    }
    [Fact]
    public async Task ApplyAsync_WritesAReportOfWhatItDid()
    {
        var loggerFactory = NullLoggerFactory.Instance;
        var revertManager = new RevertManager(
            NullLogger<RevertManager>.Instance,
            TestShell.New(),
            new PowerPlanService(NullLogger<PowerPlanService>.Instance),
            TimeProvider.System
        );
        var optimizationService = new OptimizationService(
            revertManager,
            loggerFactory,
            new SystemInfoService(NullLogger<SystemInfoService>.Instance),
            new StreamService(NullLogger<StreamService>.Instance),
            null!,
            new ShellService(new ProcessRunner(120000)),
            new PowerPlanService(NullLogger<PowerPlanService>.Instance),
            new SystemRestoreService(NullLogger<SystemRestoreService>.Instance),
            NullLogger<OptimizationService>.Instance
        );
        var optimization = new PartialFailureOptimization(shouldFail: false);
        _testOptimizationIds.Add(optimization.Id);

        var result = await optimizationService.ApplyAsync(
            optimization,
            new Progress<ProcessingProgress>(),
            TestContext.Current.CancellationToken
        );

        var record = ChangeRecordStore.TryRead(optimization.Id);
        Assert.NotNull(record);
        Assert.Equal(result.Status.ToString(), record.Outcome);
        Assert.Equal(2, record.ChangedCount);
        Assert.Equal(0, record.FailedCount);
    }

    [Fact]
    public async Task ApplyAsync_NothingToDo_WritesAReportWithoutRevertData()
    {
        var loggerFactory = NullLoggerFactory.Instance;
        var revertManager = new RevertManager(
            NullLogger<RevertManager>.Instance,
            TestShell.New(),
            new PowerPlanService(NullLogger<PowerPlanService>.Instance),
            TimeProvider.System
        );
        var optimizationService = new OptimizationService(
            revertManager,
            loggerFactory,
            new SystemInfoService(NullLogger<SystemInfoService>.Instance),
            new StreamService(NullLogger<StreamService>.Instance),
            null!,
            new ShellService(new ProcessRunner(120000)),
            new PowerPlanService(NullLogger<PowerPlanService>.Instance),
            new SystemRestoreService(NullLogger<SystemRestoreService>.Instance),
            NullLogger<OptimizationService>.Instance
        );
        var optimization = new NothingToDoOptimization();
        _testOptimizationIds.Add(optimization.Id);

        var result = await optimizationService.ApplyAsync(
            optimization,
            new Progress<ProcessingProgress>(),
            TestContext.Current.CancellationToken
        );

        // Nothing changed, so there is nothing to undo, but the run is still recorded.
        Assert.Equal(OptimizationSuccessResult.NothingToDo, result.Status);
        Assert.Null(await RevertManager.GetRevertDataAsync(optimization.Id));
        var record = ChangeRecordStore.TryRead(optimization.Id);
        Assert.NotNull(record);
        Assert.Single(record.Steps);
        Assert.Equal(ChangeKind.Skip, record.Steps[0].Kind);
    }
    [Fact]
    public async Task RevertAsync_MarksTheRecordAsARevert()
    {
        var loggerFactory = NullLoggerFactory.Instance;
        var revertManager = new RevertManager(
            NullLogger<RevertManager>.Instance,
            TestShell.New(),
            new PowerPlanService(NullLogger<PowerPlanService>.Instance),
            TimeProvider.System
        );
        var optimizationService = new OptimizationService(
            revertManager,
            loggerFactory,
            new SystemInfoService(NullLogger<SystemInfoService>.Instance),
            new StreamService(NullLogger<StreamService>.Instance),
            null!,
            new ShellService(new ProcessRunner(120000)),
            new PowerPlanService(NullLogger<PowerPlanService>.Instance),
            new SystemRestoreService(NullLogger<SystemRestoreService>.Instance),
            NullLogger<OptimizationService>.Instance
        );
        var key = $@"HKCU\Software\TestOptimizerDuckReport\{Guid.NewGuid():N}";
        var optimization = new RegistryStepOptimization { KeyPath = key };
        _testOptimizationIds.Add(optimization.Id);

        try
        {
            await optimizationService.ApplyAsync(
                optimization,
                new Progress<ProcessingProgress>(),
                TestContext.Current.CancellationToken
            );
            Assert.Equal(
                ChangeRecordOperation.Apply,
                ChangeRecordStore.TryRead(optimization.Id)?.Operation
            );

            var revert = await optimizationService.RevertAsync(
                optimization,
                new Progress<ProcessingProgress>(),
                TestContext.Current.CancellationToken
            );

            Assert.True(revert.Success, revert.Message);
            var record = ChangeRecordStore.TryRead(optimization.Id);
            Assert.NotNull(record);
            Assert.Equal(ChangeRecordOperation.Revert, record.Operation);
            Assert.NotNull(record.RevertedAt);
            Assert.Equal(1, record.ChangedCount);

            // The record describes what was undone, and the machine really is back.
            Assert.Equal(0, RegistryService.Read<int>(new RegistryItem(key, "Reported")));
        }
        finally
        {
            RegistryService.DeleteSubKeyTree(
                new OpCall { Logger = NullLogger.Instance },
                new RegistryItem(key)
            );
        }
    }
}
