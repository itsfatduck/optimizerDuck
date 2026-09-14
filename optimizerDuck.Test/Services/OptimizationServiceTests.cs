using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Revert;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Optimization;
using optimizerDuck.Services.Revert;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.Test.TestDoubles;
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
                ApplyImpl = args =>
                {
                    args.context.Changes.Add(
                        "Test step",
                        "Test step",
                        true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 0" }
                    );
                    return Task.FromResult(ApplyResult.True());
                },
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
    public async Task ApplyAsync_FailureMessage_StillPersistsSuccessfulSteps()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new FakeOptimization
            {
                ApplyImpl = args =>
                {
                    args.context.Changes.Add(
                        "Test step",
                        "Test step",
                        true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 0" }
                    );
                    args.context.Changes.Add("Failed step", "Failed step", false, error: "fail");
                    return Task.FromResult(ApplyResult.False("apply failed"));
                },
            };

            var revertPath = GetRevertFilePath(optimization.Id);

            try
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);

                var service = CreateService();
                var progress = new Progress<ProcessingProgress>(_ => { });

                var result = await service.ApplyAsync(optimization, progress);

                Assert.Equal(OptimizationSuccessResult.PartialSuccess, result.Status);
                Assert.True(File.Exists(revertPath));
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
                ApplyImpl = _ => Task.FromResult(ApplyResult.True()),
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
                    Steps = new RevertStepData?[]
                    {
                        new()
                        {
                            Index = 1,
                            Type = "Shell",
                            Data = new ShellRevertStep
                            {
                                ShellType = ShellType.CMD,
                                Command = "exit 0",
                            }.ToData(),
                        },
                    },
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
            var failedStep = new Change
            {
                Index = 3,
                Name = "Shell",
                Description = "failed step",
                Ok = false,
                Error = "fail",
                Retry = rc =>
                {
                    rc.Changes.Add(
                        "retried step",
                        "retried step",
                        true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 0" }
                    );
                    return Task.FromResult(OpResult.Success());
                },
            };

            var result = await OptimizationService.RetryFailedStepsWithResultsAsync(
                [failedStep],
                false,
                NullLogger.Instance
            );

            Assert.Empty(result.FailedSteps);
            Assert.Single(result.RecoveredSteps);
            Assert.Equal(3, result.RecoveredSteps[0].Index);
            Assert.NotNull(result.RecoveredSteps[0].Revert);
        });
    }

    [Fact]
    public async Task ApplyAsync_PartialSuccess_PersistsRevertStepsAtOriginalIndexes()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new FakeOptimization
            {
                ApplyImpl = args =>
                {
                    args.context.Changes.Add(
                        "step 1",
                        "step 1",
                        true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 11" }
                    );
                    args.context.Changes.Add(
                        "step 2",
                        "step 2",
                        false,
                        null,
                        "fail",
                        retry: rc =>
                        {
                            rc.Changes.Add(
                                "step 2 retry",
                                "step 2 retry",
                                true,
                                new ShellRevertStep
                                {
                                    ShellType = ShellType.CMD,
                                    Command = "exit 0",
                                }
                            );
                            return Task.FromResult(OpResult.Success());
                        }
                    );
                    args.context.Changes.Add(
                        "step 3",
                        "step 3",
                        true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 33" }
                    );
                    return Task.FromResult(ApplyResult.True());
                },
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
                // Compact layout: only the 2 successful steps persist, in order.
                Assert.Equal(2, data!.Steps.Length);
                Assert.Equal(
                    "exit 11",
                    data.Steps[0]!.Data[nameof(ShellRevertStep.Command)]?.ToString()
                );
                Assert.Equal(
                    "exit 33",
                    data.Steps[1]!.Data[nameof(ShellRevertStep.Command)]?.ToString()
                );
            }
            finally
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);
            }
        });
    }

    [Fact]
    public async Task AppendRevertStepAsync_AfterRetrySuccess_AppendsWithoutDestroyingExistingEntries()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new FakeOptimization
            {
                ApplyImpl = args =>
                {
                    args.context.Changes.Add(
                        "step 1",
                        "step 1",
                        true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 11" }
                    );
                    args.context.Changes.Add(
                        "step 2",
                        "step 2",
                        false,
                        null,
                        "fail",
                        retry: rc =>
                        {
                            rc.Changes.Add(
                                "step 2 retry",
                                "step 2 retry",
                                true,
                                new ShellRevertStep
                                {
                                    ShellType = ShellType.CMD,
                                    Command = "exit 0",
                                }
                            );
                            return Task.FromResult(OpResult.Success());
                        }
                    );
                    args.context.Changes.Add(
                        "step 3",
                        "step 3",
                        true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 33" }
                    );
                    return Task.FromResult(ApplyResult.True());
                },
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
                    NullLogger.Instance
                );

                var retriedStep = Assert.Single(retryResult.RecoveredSteps);
                Assert.Equal(2, retriedStep.Index);
                Assert.NotNull(retriedStep.Revert);

                var revertManager = new RevertManager(
                    NullLogger<RevertManager>.Instance,
                    TestShell.New(),
                    new PowerPlanService(NullLogger<PowerPlanService>.Instance),
                    TimeProvider.System
                );
                await revertManager.AppendRevertStepAsync(
                    optimization.Id,
                    optimization.OptimizationKey,
                    retriedStep.Revert!
                );

                var data = await RevertManager.GetRevertDataAsync(optimization.Id);

                Assert.NotNull(data);
                // Save wrote [exit 11, exit 33]; the recovered step appends.
                Assert.Equal(3, data!.Steps.Length);
                Assert.Equal(
                    "exit 11",
                    data.Steps[0]!.Data[nameof(ShellRevertStep.Command)]?.ToString()
                );
                Assert.Equal(
                    "exit 33",
                    data.Steps[1]!.Data[nameof(ShellRevertStep.Command)]?.ToString()
                );
                Assert.Equal(
                    "exit 0",
                    data.Steps[2]!.Data[nameof(ShellRevertStep.Command)]?.ToString()
                );
            }
            finally
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);
            }
        });
    }

    [Fact]
    public async Task AppendRevertStepAsync_AfterMultipleRetrySuccesses_AppendsInRetryOrder()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new FakeOptimization
            {
                ApplyImpl = args =>
                {
                    args.context.Changes.Add(
                        "step 1",
                        "step 1",
                        true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 11" }
                    );
                    args.context.Changes.Add(
                        "step 2",
                        "step 2",
                        false,
                        null,
                        "fail 2",
                        retry: rc =>
                        {
                            rc.Changes.Add(
                                "step 2 retry",
                                "step 2 retry",
                                true,
                                new ShellRevertStep
                                {
                                    ShellType = ShellType.CMD,
                                    Command = "exit 22",
                                }
                            );
                            return Task.FromResult(OpResult.Success());
                        }
                    );
                    args.context.Changes.Add(
                        "step 3",
                        "step 3",
                        true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 33" }
                    );
                    args.context.Changes.Add(
                        "step 4",
                        "step 4",
                        false,
                        null,
                        "fail 4",
                        retry: rc =>
                        {
                            rc.Changes.Add(
                                "step 4 retry",
                                "step 4 retry",
                                true,
                                new ShellRevertStep
                                {
                                    ShellType = ShellType.CMD,
                                    Command = "exit 44",
                                }
                            );
                            return Task.FromResult(OpResult.Success());
                        }
                    );
                    return Task.FromResult(ApplyResult.True());
                },
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
                    NullLogger.Instance
                );

                Assert.Empty(retryResult.FailedSteps);
                Assert.Equal(
                    [2, 4],
                    retryResult.RecoveredSteps.Select(step => step.Index).ToArray()
                );

                var revertManager = new RevertManager(
                    NullLogger<RevertManager>.Instance,
                    TestShell.New(),
                    new PowerPlanService(NullLogger<PowerPlanService>.Instance),
                    TimeProvider.System
                );
                foreach (var recoveredStep in retryResult.RecoveredSteps)
                    await revertManager.AppendRevertStepAsync(
                        optimization.Id,
                        optimization.OptimizationKey,
                        recoveredStep.Revert!
                    );

                var data = await RevertManager.GetRevertDataAsync(optimization.Id);

                Assert.NotNull(data);
                Assert.Equal(4, data!.Steps.Length);
                var commands = data
                    .Steps.Where(step => step != null)
                    .Select(step => step!.Data[nameof(ShellRevertStep.Command)]!.ToString())
                    .ToArray();
                // Save wrote [exit 11, exit 33]; recovered steps append in retry order.
                Assert.Equal(["exit 11", "exit 33", "exit 22", "exit 44"], commands);
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
            var failedStep = new Change
            {
                Index = 2,
                Name = "Shell",
                Description = "still failing step",
                Ok = false,
                Error = "initial error",
                Retry = _ => throw new InvalidOperationException("retry exploded"),
            };

            var result = await OptimizationService.RetryFailedStepsWithResultsAsync(
                [failedStep],
                false,
                NullLogger.Instance
            );

            var stillFailedStep = Assert.Single(result.FailedSteps);
            Assert.Empty(result.RecoveredSteps);
            Assert.Equal(2, stillFailedStep.Index);
            Assert.Equal("retry exploded", stillFailedStep.Error);
        });
    }

    private static OptimizationService CreateService()
    {
        var revertManager = new RevertManager(
            NullLogger<RevertManager>.Instance,
            TestShell.New(),
            new PowerPlanService(NullLogger<PowerPlanService>.Instance),
            TimeProvider.System
        );
        var loggerFactory = NullLoggerFactory.Instance;
        var systemInfoService = new SystemInfoService(NullLogger<SystemInfoService>.Instance);
        var streamService = new StreamService(NullLogger<StreamService>.Instance);
        var contentDialogService = new ContentDialogService();
        var shellService = new ShellService(new ProcessRunner(120000));
        var logger = NullLogger<OptimizationService>.Instance;
        return new OptimizationService(
            revertManager,
            loggerFactory,
            systemInfoService,
            streamService,
            contentDialogService,
            shellService,
            new PowerPlanService(NullLogger<PowerPlanService>.Instance),
            new SystemRestoreService(NullLogger<SystemRestoreService>.Instance),
            logger
        );
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

    private sealed class FakeOptimization : StubOptimization
    {
        public Type? OwnerType { get; set; }
        public string OwnerKey { get; } = "Test";
        public RiskVisual RiskVisual { get; } = new();
        public IEnumerable<OptimizationTagDisplay> TagDisplays { get; } = [];

        public string Prefix { get; } = "Test";
        public string ProgressPrefix { get; } = "Test";

        public Func<
            (IProgress<ProcessingProgress> progress, OptimizationContext context),
            Task<ApplyResult>
        > ApplyImpl { get; init; } = _ => Task.FromResult(ApplyResult.True());

        public override string Name => "Test";
        public override string ShortDescription => "Test";

        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            return ApplyImpl((progress, context));
        }
    }
}
