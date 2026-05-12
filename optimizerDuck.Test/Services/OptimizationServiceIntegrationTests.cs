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
using optimizerDuck.Domain.Revert;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services;
using optimizerDuck.Services.Managers;
using Xunit;

namespace optimizerDuck.Test.Services;

public class OptimizationServiceIntegrationTests : IDisposable
{
    private class PartialFailureOptimization : IOptimization
    {
        private int _stepCount;
        private readonly bool _shouldFail;

        public Guid Id { get; } = Guid.NewGuid();
        public OptimizationRisk Risk => OptimizationRisk.Safe;
        public string OptimizationKey => "PartialFailureTest";
        public string Name => "Partial Failure Test";
        public string ShortDescription => "Tests partial failure scenarios";
        public OptimizationState State { get; set; } = new();

        public PartialFailureOptimization(bool shouldFail = false)
        {
            _shouldFail = shouldFail;
        }

        public Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            var results = new List<OperationStepResult>();
            _stepCount = 0;

            // Step 1: Always succeeds
            ExecutionScope.RecordStep(
                "Test",
                "Step 1",
                true,
                new TestRevertStep { StepId = 1 }
            );
            _stepCount++;

            // Step 2: May fail
            if (_shouldFail)
            {
                ExecutionScope.RecordStep(
                    "Test",
                    "Step 2",
                    false,
                    null,
                    "Simulated failure"
                );
                _stepCount++;
            }

            // Step 3: Only executes if step 2 succeeded
            if (!_shouldFail)
            {
                ExecutionScope.RecordStep(
                    "Test",
                    "Step 3",
                    true,
                    new TestRevertStep { StepId = 3 }
                );
                _stepCount++;
            }

            var success = !_shouldFail;
            return Task.FromResult(
                success
                    ? ApplyResult.True()
                    : ApplyResult.False("Step 2 failed intentionally")
            );
        }
    }

    private class TestRevertStep : IRevertStep
    {
        public int StepId { get; init; }
        public string Type => "Test";
        public string Description => $"Revert step {StepId}";

        public Task<bool> ExecuteAsync()
        {
            // Simulate revert operation
            return Task.FromResult(true);
        }

        public JObject ToData()
        {
            return new JObject { ["StepId"] = StepId };
        }
    }

    public OptimizationServiceIntegrationTests()
    {
        // Ensure clean revert directory
        if (Directory.Exists(Shared.RevertDirectory))
        {
            foreach (var file in Directory.GetFiles(Shared.RevertDirectory))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore
                }
            }
        }
    }

    public void Dispose()
    {
        // Clean up test files
        if (Directory.Exists(Shared.RevertDirectory))
        {
            foreach (var file in Directory.GetFiles(Shared.RevertDirectory))
            {
                try
                {
                    File.Delete(file);
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
        var revertManager = new RevertManager(NullLogger<RevertManager>.Instance, loggerFactory);
        var systemInfoService = new SystemInfoService(NullLogger<SystemInfoService>.Instance);
        var streamService = new StreamService(NullLogger<StreamService>.Instance);

        var optimizationService = new OptimizationService(
            revertManager,
            loggerFactory,
            systemInfoService,
            streamService,
            null!,
            NullLogger<OptimizationService>.Instance
        );

        var optimization = new PartialFailureOptimization(shouldFail: false);
        var progress = new Progress<ProcessingProgress>();

        var result = await optimizationService.ApplyAsync(optimization, progress);

        Assert.Equal(OptimizationSuccessResult.Success, result.Status);

        // Verify revert data was saved
        var revertData = await RevertManager.GetRevertDataAsync(optimization.Id);
        Assert.NotNull(revertData);
        Assert.Equal(2, revertData.Steps.Count); // 2 successful steps
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
        var failedSteps = new List<OperationStepResult>
        {
            new()
            {
                Index = 1,
                Name = "Test",
                Description = "Failed step",
                Success = false,
                Error = "Temporary failure",
                RetryAction = () => Task.FromResult(true),
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
        var failedSteps = new List<OperationStepResult>
        {
            new()
            {
                Index = 1,
                Name = "Test",
                Description = "Failed step",
                Success = false,
                Error = "Permanent failure",
                RetryAction = null, // No retry action
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
}
