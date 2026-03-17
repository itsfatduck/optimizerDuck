using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Revert;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services;
using optimizerDuck.Services.Managers;
using Wpf.Ui;

namespace optimizerDuck.Test.Services;

public class OptimizationServiceTests
{
    [Fact]
    public async Task ApplyAsync_Success_PersistsRevertDataFile()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new FakeOptimization
            {
                ApplyImpl = _ =>
                {
                    ExecutionScope.RecordStep("Test", "Test step", true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 0" });
                    return Task.FromResult(ApplyResult.True());
                }
            };

            var revertPath = GetRevertFilePath(optimization.Id);

            try
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);

                var service = CreateService();
                var progress = new Progress<ProcessingProgress>(_ => { });

                var result = await service.ApplyAsync(optimization, progress);

                Assert.Equal(OptimizationSuccessResult.Success, result.Status);
                Assert.True(File.Exists(revertPath));

                var data = await RevertManager.GetRevertDataAsync(optimization.Id);
                Assert.NotNull(data);
                Assert.Equal(optimization.Id, data!.OptimizationId);
                Assert.NotEmpty(data.Steps);
            }
            finally
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);
            }
        });
    }

    [Fact]
    public async Task ApplyAsync_FailureMessage_DoesNotPersistRevertDataFile()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new FakeOptimization
            {
                ApplyImpl = _ =>
                {
                    ExecutionScope.RecordStep("Test", "Test step", true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 0" });
                    ExecutionScope.RecordStep("Test", "Failed step", false, error: "fail");
                    return Task.FromResult(ApplyResult.False("apply failed"));
                }
            };

            var revertPath = GetRevertFilePath(optimization.Id);

            try
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);

                var service = CreateService();
                var progress = new Progress<ProcessingProgress>(_ => { });

                var result = await service.ApplyAsync(optimization, progress);

                Assert.Equal(OptimizationSuccessResult.Failed, result.Status);
                Assert.False(File.Exists(revertPath));
            }
            finally
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);
            }
        });
    }

    [Fact]
    public async Task RevertAsync_WithValidRevertData_RemovesFile()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new FakeOptimization
            {
                ApplyImpl = _ => Task.FromResult(ApplyResult.True())
            };

            var revertPath = GetRevertFilePath(optimization.Id);

            try
            {
                Directory.CreateDirectory(Shared.RevertDirectory);

                var payload = new RevertData
                {
                    OptimizationId = optimization.Id,
                    OptimizationName = optimization.OptimizationKey,
                    AppliedAt = DateTime.UtcNow,
                    Steps = new List<RevertStepData>
                    {
                        new()
                        {
                            Type = "Shell",
                            Data = new ShellRevertStep
                            {
                                ShellType = ShellType.CMD,
                                Command = "exit 0"
                            }.ToData()
                        }
                    }
                };

                var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
                await File.WriteAllTextAsync(revertPath, json);

                var service = CreateService();
                var progress = new Progress<ProcessingProgress>(_ => { });

                var result = await service.RevertAsync(optimization, progress);

                Assert.True(result.Success);
                Assert.False(File.Exists(revertPath));
            }
            finally
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);
            }
        });
    }

    [Fact]
    public async Task RetryFailedStepsWithResultsAsync_WhenRetrySucceeds_ReturnsRevertStepForOriginalIndex()
    {
        await RunInStaThreadAsync(async () =>
        {
            var failedStep = new OperationStepResult
            {
                Index = 3,
                Name = "Shell",
                Description = "failed step",
                Success = false,
                Error = "fail",
                RetryAction = () =>
                {
                    ExecutionScope.RecordStep("Shell", "retried step", true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 0" });
                    return Task.FromResult(true);
                }
            };

            var result = await OptimizationService.RetryFailedStepsWithResultsAsync(
                [failedStep],
                false,
                NullLogger.Instance);

            Assert.Empty(result.FailedSteps);
            Assert.Single(result.RecoveredSteps);
            Assert.Equal(3, result.RecoveredSteps[0].Index);
            Assert.NotNull(result.RecoveredSteps[0].RevertStep);
        });
    }

    [Fact]
    public async Task ApplyAsync_PartialSuccess_PersistsRevertStepsAtOriginalIndexes()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new FakeOptimization
            {
                ApplyImpl = _ =>
                {
                    ExecutionScope.RecordStep("Shell", "step 1", true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 11" });
                    ExecutionScope.RecordStep("Shell", "step 2", false, null, "fail", () =>
                    {
                        ExecutionScope.RecordStep("Shell", "step 2 retry", true,
                            new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 0" });
                        return Task.FromResult(true);
                    });
                    ExecutionScope.RecordStep("Shell", "step 3", true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 33" });
                    return Task.FromResult(ApplyResult.True());
                }
            };

            var revertPath = GetRevertFilePath(optimization.Id);

            try
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);

                var service = CreateService();
                var progress = new Progress<ProcessingProgress>(_ => { });

                var result = await service.ApplyAsync(optimization, progress);
                var data = await RevertManager.GetRevertDataAsync(optimization.Id);

                Assert.Equal(OptimizationSuccessResult.PartialSuccess, result.Status);
                Assert.NotNull(data);
                Assert.Equal(3, data!.Steps.Count);
                Assert.Equal("Shell", data.Steps[0].Type);
                Assert.Equal("Unknown", data.Steps[1].Type);
                Assert.Equal("Shell", data.Steps[2].Type);
                Assert.Equal("exit 11", data.Steps[0].Data[nameof(ShellRevertStep.Command)]?.ToString());
                Assert.Equal("exit 33", data.Steps[2].Data[nameof(ShellRevertStep.Command)]?.ToString());
            }
            finally
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);
            }
        });
    }

    [Fact]
    public async Task UpsertRevertStepAtIndexAsync_AfterRetrySuccess_InsertsStepAtFailedIndexWithoutShiftingLaterSteps()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new FakeOptimization
            {
                ApplyImpl = _ =>
                {
                    ExecutionScope.RecordStep("Shell", "step 1", true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 11" });
                    ExecutionScope.RecordStep("Shell", "step 2", false, null, "fail", () =>
                    {
                        ExecutionScope.RecordStep("Shell", "step 2 retry", true,
                            new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 0" });
                        return Task.FromResult(true);
                    });
                    ExecutionScope.RecordStep("Shell", "step 3", true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 33" });
                    return Task.FromResult(ApplyResult.True());
                }
            };

            var revertPath = GetRevertFilePath(optimization.Id);

            try
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);

                var service = CreateService();
                var progress = new Progress<ProcessingProgress>(_ => { });
                var applyResult = await service.ApplyAsync(optimization, progress);

                Assert.Equal(OptimizationSuccessResult.PartialSuccess, applyResult.Status);

                var retryResult = await OptimizationService.RetryFailedStepsWithResultsAsync(
                    applyResult.FailedSteps,
                    false,
                    NullLogger.Instance);

                var retriedStep = Assert.Single(retryResult.RecoveredSteps);
                Assert.Equal(2, retriedStep.Index);
                Assert.NotNull(retriedStep.RevertStep);

                var revertManager = new RevertManager(NullLogger<RevertManager>.Instance, NullLoggerFactory.Instance);
                await revertManager.UpsertRevertStepAtIndexAsync(
                    optimization.Id,
                    optimization.OptimizationKey,
                    retriedStep.Index,
                    retriedStep.RevertStep!);

                var data = await RevertManager.GetRevertDataAsync(optimization.Id);

                Assert.NotNull(data);
                Assert.Equal(3, data!.Steps.Count);
                Assert.Equal("exit 11", data.Steps[0].Data[nameof(ShellRevertStep.Command)]?.ToString());
                Assert.Equal("exit 0", data.Steps[1].Data[nameof(ShellRevertStep.Command)]?.ToString());
                Assert.Equal("exit 33", data.Steps[2].Data[nameof(ShellRevertStep.Command)]?.ToString());
            }
            finally
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);
            }
        });
    }

    [Fact]
    public async Task UpsertRevertStepAtIndexAsync_AfterMultipleRetrySuccesses_PreservesOriginalOrder()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new FakeOptimization
            {
                ApplyImpl = _ =>
                {
                    ExecutionScope.RecordStep("Shell", "step 1", true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 11" });
                    ExecutionScope.RecordStep("Shell", "step 2", false, null, "fail 2", () =>
                    {
                        ExecutionScope.RecordStep("Shell", "step 2 retry", true,
                            new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 22" });
                        return Task.FromResult(true);
                    });
                    ExecutionScope.RecordStep("Shell", "step 3", true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 33" });
                    ExecutionScope.RecordStep("Shell", "step 4", false, null, "fail 4", () =>
                    {
                        ExecutionScope.RecordStep("Shell", "step 4 retry", true,
                            new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 44" });
                        return Task.FromResult(true);
                    });
                    return Task.FromResult(ApplyResult.True());
                }
            };

            var revertPath = GetRevertFilePath(optimization.Id);

            try
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);

                var service = CreateService();
                var progress = new Progress<ProcessingProgress>(_ => { });
                var applyResult = await service.ApplyAsync(optimization, progress);

                Assert.Equal(OptimizationSuccessResult.PartialSuccess, applyResult.Status);
                Assert.Equal([2, 4], applyResult.FailedSteps.Select(step => step.Index).ToArray());

                var retryResult = await OptimizationService.RetryFailedStepsWithResultsAsync(
                    applyResult.FailedSteps,
                    false,
                    NullLogger.Instance);

                Assert.Empty(retryResult.FailedSteps);
                Assert.Equal([2, 4], retryResult.RecoveredSteps.Select(step => step.Index).ToArray());

                var revertManager = new RevertManager(NullLogger<RevertManager>.Instance, NullLoggerFactory.Instance);
                foreach (var recoveredStep in retryResult.RecoveredSteps)
                    await revertManager.UpsertRevertStepAtIndexAsync(
                        optimization.Id,
                        optimization.OptimizationKey,
                        recoveredStep.Index,
                        recoveredStep.RevertStep!);

                var data = await RevertManager.GetRevertDataAsync(optimization.Id);

                Assert.NotNull(data);
                Assert.Equal(4, data!.Steps.Count);
                var commands = data.Steps
                    .Select(step => step.Data[nameof(ShellRevertStep.Command)]!.ToString())
                    .ToArray();
                Assert.Equal(["exit 11", "exit 22", "exit 33", "exit 44"],
                    commands);
            }
            finally
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);
            }
        });
    }

    [Fact]
    public async Task RetryFailedStepsWithResultsAsync_WhenRetryStillFails_ReturnsUpdatedFailedStep()
    {
        await RunInStaThreadAsync(async () =>
        {
            var failedStep = new OperationStepResult
            {
                Index = 2,
                Name = "Shell",
                Description = "still failing step",
                Success = false,
                Error = "initial error",
                RetryAction = () => throw new InvalidOperationException("retry exploded")
            };

            var result = await OptimizationService.RetryFailedStepsWithResultsAsync(
                [failedStep],
                false,
                NullLogger.Instance);

            var stillFailedStep = Assert.Single(result.FailedSteps);
            Assert.Empty(result.RecoveredSteps);
            Assert.Equal(2, stillFailedStep.Index);
            Assert.Equal("retry exploded", stillFailedStep.Error);
        });
    }

    private static OptimizationService CreateService()
    {
        var revertManager = new RevertManager(NullLogger<RevertManager>.Instance, NullLoggerFactory.Instance);
        var loggerFactory = NullLoggerFactory.Instance;
        var systemInfoService = new SystemInfoService(NullLogger<SystemInfoService>.Instance);
        var streamService = new StreamService(NullLogger<StreamService>.Instance);
        var contentDialogService = new ContentDialogService();
        var logger = NullLogger<OptimizationService>.Instance;
        return new OptimizationService(revertManager, loggerFactory, systemInfoService, streamService,
            contentDialogService, logger);
    }

    private static string GetRevertFilePath(Guid id)
    {
        return Path.Combine(Shared.RevertDirectory, id + ".json");
    }

    private static Task RunInStaThreadAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource();

        var thread = new Thread(() =>
        {
            try
            {
                action().GetAwaiter().GetResult();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return tcs.Task;
    }

    private sealed class FakeOptimization : IOptimization
    {
        public Type? OwnerType { get; set; }
        public string OwnerKey { get; } = "Test";
        public RiskVisual RiskVisual { get; } = new();
        public IEnumerable<OptimizationTagDisplay> TagDisplays { get; } = [];

        public string Prefix { get; } = "Test";
        public string ProgressPrefix { get; } = "Test";

        public Func<(IProgress<ProcessingProgress> progress, OptimizationContext context), Task<ApplyResult>> ApplyImpl
        {
            get;
            init;
        } =
            _ => Task.FromResult(ApplyResult.True());

        public Guid Id { get; } = Guid.NewGuid();
        public OptimizationRisk Risk { get; } = OptimizationRisk.Safe;
        public string OptimizationKey { get; } = "TestOptimization";

        public string Name { get; } = "Test";
        public string ShortDescription { get; } = "Test";
        public OptimizationState State { get; set; } = new();

        public Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            return ApplyImpl((progress, context));
        }
    }
}
