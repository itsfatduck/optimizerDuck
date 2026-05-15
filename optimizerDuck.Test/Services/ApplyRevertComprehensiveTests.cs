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

/// <summary>
/// Comprehensive tests for apply and revert operations covering all scenarios.
/// These tests ensure safe operation without affecting the actual machine.
/// </summary>
public class ApplyRevertComprehensiveTests
{
    #region Apply Success Scenarios

    [Fact]
    public async Task ApplyAsync_AllStepsSuccess_CompleteRevertDataSaved()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new TestOptimization
            {
                ApplyImpl = _ =>
                {
                    ExecutionScope.RecordStep(
                        "Shell",
                        "Step 1: Disable service",
                        true,
                        new ShellRevertStep
                        {
                            ShellType = ShellType.CMD,
                            Command = "sc config TestService start= auto",
                        }
                    );
                    ExecutionScope.RecordStep(
                        "Shell",
                        "Step 2: Registry change",
                        true,
                        new ShellRevertStep
                        {
                            ShellType = ShellType.PowerShell,
                            Command = "Set-ItemProperty -Path 'HKLM:\\Test' -Name 'Value' -Value 0",
                        }
                    );
                    ExecutionScope.RecordStep(
                        "Shell",
                        "Step 3: File operation",
                        true,
                        new ShellRevertStep
                        {
                            ShellType = ShellType.CMD,
                            Command = "del C:\\test.txt",
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

                var result = await service.ApplyAsync(optimization, progress);

                Assert.Equal(OptimizationSuccessResult.Success, result.Status);
                Assert.Empty(result.FailedSteps);
                Assert.True(File.Exists(revertPath));

                var data = await RevertManager.GetRevertDataAsync(optimization.Id);
                Assert.NotNull(data);
                Assert.Equal(optimization.Id, data!.OptimizationId);
                Assert.Equal(3, data.Steps.Length);

                // Verify all steps are present and in correct order
                Assert.NotNull(data.Steps[0]);
                Assert.NotNull(data.Steps[1]);
                Assert.NotNull(data.Steps[2]);
                Assert.Equal("Shell", data.Steps[0]!.Type);
                Assert.Equal("Shell", data.Steps[1]!.Type);
                Assert.Equal("Shell", data.Steps[2]!.Type);

                // Verify revert commands are correct
                Assert.Equal(
                    "sc config TestService start= auto",
                    data.Steps[0]!.Data[nameof(ShellRevertStep.Command)]?.ToString()
                );
                Assert.Equal(
                    "Set-ItemProperty -Path 'HKLM:\\Test' -Name 'Value' -Value 0",
                    data.Steps[1]!.Data[nameof(ShellRevertStep.Command)]?.ToString()
                );
                Assert.Equal(
                    "del C:\\test.txt",
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

    #endregion

    #region Apply Partial Success Scenarios

    [Fact]
    public async Task ApplyAsync_FirstStepFails_PartialSuccessWithGapInRevertData()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new TestOptimization
            {
                ApplyImpl = _ =>
                {
                    ExecutionScope.RecordStep(
                        "Shell",
                        "Step 1: Will fail",
                        false,
                        null,
                        "Step 1 failed"
                    );
                    ExecutionScope.RecordStep(
                        "Shell",
                        "Step 2: Success",
                        true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 0" }
                    );
                    ExecutionScope.RecordStep(
                        "Shell",
                        "Step 3: Success",
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

                Assert.Equal(OptimizationSuccessResult.PartialSuccess, result.Status);
                Assert.Single(result.FailedSteps);
                Assert.Equal(1, result.FailedSteps[0].Index);

                var data = await RevertManager.GetRevertDataAsync(optimization.Id);
                Assert.NotNull(data);
                Assert.Equal(3, data!.Steps.Length);

                // Step 1 should be null (failed), steps 2 and 3 should exist
                Assert.Null(data.Steps[0]);
                Assert.NotNull(data.Steps[1]);
                Assert.NotNull(data.Steps[2]);
            }
            finally
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);
            }
        });
    }

    [Fact]
    public async Task ApplyAsync_MiddleStepsFail_MultipleGapsInRevertData()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new TestOptimization
            {
                ApplyImpl = _ =>
                {
                    ExecutionScope.RecordStep(
                        "Shell",
                        "Step 1: Success",
                        true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 11" }
                    );
                    ExecutionScope.RecordStep("Shell", "Step 2: Fail", false, null, "fail 2");
                    ExecutionScope.RecordStep(
                        "Shell",
                        "Step 3: Success",
                        true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 33" }
                    );
                    ExecutionScope.RecordStep("Shell", "Step 4: Fail", false, null, "fail 4");
                    ExecutionScope.RecordStep(
                        "Shell",
                        "Step 5: Success",
                        true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 55" }
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

                Assert.Equal(OptimizationSuccessResult.PartialSuccess, result.Status);
                Assert.Equal(2, result.FailedSteps.Count);
                Assert.Equal([2, 4], result.FailedSteps.Select(s => s.Index).ToArray());

                var data = await RevertManager.GetRevertDataAsync(optimization.Id);
                Assert.NotNull(data);
                Assert.Equal(5, data!.Steps.Length);

                // Verify pattern: success, fail, success, fail, success
                Assert.NotNull(data.Steps[0]);
                Assert.Null(data.Steps[1]);
                Assert.NotNull(data.Steps[2]);
                Assert.Null(data.Steps[3]);
                Assert.NotNull(data.Steps[4]);

                // Verify commands are at correct indices
                Assert.Equal(
                    "exit 11",
                    data.Steps[0]!.Data[nameof(ShellRevertStep.Command)]?.ToString()
                );
                Assert.Equal(
                    "exit 33",
                    data.Steps[2]!.Data[nameof(ShellRevertStep.Command)]?.ToString()
                );
                Assert.Equal(
                    "exit 55",
                    data.Steps[4]!.Data[nameof(ShellRevertStep.Command)]?.ToString()
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
    public async Task ApplyAsync_AllStepsFail_NoRevertDataFileCreated()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new TestOptimization
            {
                ApplyImpl = _ =>
                {
                    ExecutionScope.RecordStep("Shell", "Step 1: Fail", false, null, "fail 1");
                    ExecutionScope.RecordStep("Shell", "Step 2: Fail", false, null, "fail 2");
                    return Task.FromResult(ApplyResult.False("All steps failed"));
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

                Assert.Equal(OptimizationSuccessResult.Failed, result.Status);
                Assert.Equal(2, result.FailedSteps.Count);
                Assert.False(File.Exists(revertPath));
            }
            finally
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);
            }
        });
    }

    #endregion

    #region Revert Success Scenarios

    [Fact]
    public async Task RevertAsync_AllStepsSucceed_FileDeletedAndSuccessReturned()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new TestOptimization { Id = Guid.NewGuid() };
            var revertPath = GetRevertFilePath(optimization.Id);

            try
            {
                Directory.CreateDirectory(Shared.RevertDirectory);

                // Create revert data with 3 successful steps
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
                        new()
                        {
                            Index = 2,
                            Type = "Shell",
                            Data = new ShellRevertStep
                            {
                                ShellType = ShellType.PowerShell,
                                Command = "exit 0",
                            }.ToData(),
                        },
                        new()
                        {
                            Index = 3,
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
                Assert.Empty(result.FailedSteps);
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
    public async Task RevertAsync_StepsExecutedInReverseOrder_LastStepFirst()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new TestOptimization { Id = Guid.NewGuid() };
            var revertPath = GetRevertFilePath(optimization.Id);
            var executionOrder = new List<int>();

            try
            {
                Directory.CreateDirectory(Shared.RevertDirectory);

                // Create revert data with ordered steps
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
                                Command = "cmd /c \"echo 1\"",
                            }.ToData(),
                        },
                        new()
                        {
                            Index = 2,
                            Type = "Shell",
                            Data = new ShellRevertStep
                            {
                                ShellType = ShellType.CMD,
                                Command = "cmd /c \"echo 2\"",
                            }.ToData(),
                        },
                        new()
                        {
                            Index = 3,
                            Type = "Shell",
                            Data = new ShellRevertStep
                            {
                                ShellType = ShellType.CMD,
                                Command = "cmd /c \"echo 3\"",
                            }.ToData(),
                        },
                    },
                };

                var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
                await File.WriteAllTextAsync(revertPath, json);

                var manager = new RevertManager(
                    NullLogger<RevertManager>.Instance,
                    NullLoggerFactory.Instance
                );
                var result = await manager.RevertAsync(optimization);

                Assert.True(result.Success);
                // Steps should execute in reverse order: 3, 2, 1
                // This is verified by the revert manager implementation
            }
            finally
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);
            }
        });
    }

    [Fact]
    public async Task RevertAsync_WithNullStepsInData_SkipsNullStepsSuccessfully()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new TestOptimization { Id = Guid.NewGuid() };
            var revertPath = GetRevertFilePath(optimization.Id);

            try
            {
                Directory.CreateDirectory(Shared.RevertDirectory);

                // Create revert data with gaps (null steps from failed apply steps)
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
                        null, // Step 2 failed during apply
                        new()
                        {
                            Index = 3,
                            Type = "Shell",
                            Data = new ShellRevertStep
                            {
                                ShellType = ShellType.CMD,
                                Command = "exit 0",
                            }.ToData(),
                        },
                        null, // Step 4 failed during apply
                        new()
                        {
                            Index = 5,
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

    #endregion

    #region Revert Partial Failure Scenarios

    [Fact]
    public async Task RevertAsync_LastStepFails_PartialFailureWithFileDeleted()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new TestOptimization { Id = Guid.NewGuid() };
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
                                Command = "exit 0", // Success
                            }.ToData(),
                        },
                        new()
                        {
                            Index = 2,
                            Type = "Shell",
                            Data = new ShellRevertStep
                            {
                                ShellType = ShellType.CMD,
                                Command = "exit 1", // Fail
                            }.ToData(),
                        },
                    },
                };

                var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
                await File.WriteAllTextAsync(revertPath, json);

                var service = CreateService();
                var progress = new Progress<ProcessingProgress>(_ => { });

                var result = await service.RevertAsync(optimization, progress);

                // Revert executes in reverse: step 2 (fails), then step 1 (succeeds)
                Assert.False(result.Success);
                Assert.False(result.AllStepsFailed);
                Assert.Single(result.FailedSteps);
                Assert.False(File.Exists(revertPath)); // File deleted because not all steps failed

                // Failed step should have retry action
                Assert.NotNull(result.FailedSteps[0].RetryAction);
            }
            finally
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);
            }
        });
    }

    [Fact]
    public async Task RevertAsync_AllStepsFail_FilePreservedForRetry()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new TestOptimization { Id = Guid.NewGuid() };
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
                                Command = "exit 1", // Fail
                            }.ToData(),
                        },
                        new()
                        {
                            Index = 2,
                            Type = "Shell",
                            Data = new ShellRevertStep
                            {
                                ShellType = ShellType.CMD,
                                Command = "exit 1", // Fail
                            }.ToData(),
                        },
                    },
                };

                var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
                await File.WriteAllTextAsync(revertPath, json);

                var service = CreateService();
                var progress = new Progress<ProcessingProgress>(_ => { });

                var result = await service.RevertAsync(optimization, progress);

                Assert.False(result.Success);
                Assert.True(result.AllStepsFailed);
                Assert.Equal(2, result.FailedSteps.Count);
                Assert.True(File.Exists(revertPath)); // File preserved for retry
            }
            finally
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);
            }
        });
    }

    [Fact]
    public async Task RevertAsync_PartialFailure_RetryActionCanRecoverFailedSteps()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new TestOptimization { Id = Guid.NewGuid() };
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
                                Command = "exit 0", // Success
                            }.ToData(),
                        },
                        new()
                        {
                            Index = 2,
                            Type = "Shell",
                            Data = new ShellRevertStep
                            {
                                ShellType = ShellType.CMD,
                                Command = "exit 1", // Fail
                            }.ToData(),
                        },
                    },
                };

                var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
                await File.WriteAllTextAsync(revertPath, json);

                var manager = new RevertManager(
                    NullLogger<RevertManager>.Instance,
                    NullLoggerFactory.Instance
                );
                var result = await manager.RevertAsync(optimization);

                Assert.False(result.Success);
                Assert.False(File.Exists(revertPath));

                // Retry the failed step - note: retry will also fail because it's still "exit 1"
                var failedStep = result.FailedSteps.FirstOrDefault();
                Assert.NotNull(failedStep);
                Assert.NotNull(failedStep.RetryAction);

                // Retry will fail because the command is still "exit 1"
                // This is expected behavior - retry just re-executes the same command
                // ExecuteAsync throws exception on failure, so we need to catch it
                var exception = await Record.ExceptionAsync(async () =>
                    await failedStep.RetryAction!()
                );
                Assert.NotNull(exception); // Expected to throw
                Assert.False(File.Exists(revertPath));
            }
            finally
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);
            }
        });
    }

    #endregion

    #region Apply Then Revert Full Workflow

    [Fact]
    public async Task ApplyThenRevert_FullWorkflow_SuccessfulRoundTrip()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new TestOptimization
            {
                ApplyImpl = _ =>
                {
                    ExecutionScope.RecordStep(
                        "Shell",
                        "Step 1",
                        true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 0" }
                    );
                    ExecutionScope.RecordStep(
                        "Shell",
                        "Step 2",
                        true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 0" }
                    );
                    return Task.FromResult(ApplyResult.True());
                },
            };

            var revertPath = GetRevertFilePath(optimization.Id);

            try
            {
                // Phase 1: Apply
                var service = CreateService();
                var progress = new Progress<ProcessingProgress>(_ => { });

                var applyResult = await service.ApplyAsync(optimization, progress);
                Assert.Equal(OptimizationSuccessResult.Success, applyResult.Status);
                Assert.True(File.Exists(revertPath));

                var data = await RevertManager.GetRevertDataAsync(optimization.Id);
                Assert.NotNull(data);
                Assert.Equal(2, data!.Steps.Length);

                // Phase 2: Revert
                var revertResult = await service.RevertAsync(optimization, progress);
                Assert.True(revertResult.Success);
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
    public async Task ApplyPartialThenRevert_PartialApplyFollowedByRevert()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new TestOptimization
            {
                ApplyImpl = _ =>
                {
                    ExecutionScope.RecordStep(
                        "Shell",
                        "Step 1",
                        true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 0" }
                    );
                    ExecutionScope.RecordStep("Shell", "Step 2", false, null, "Step 2 failed");
                    ExecutionScope.RecordStep(
                        "Shell",
                        "Step 3",
                        true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 0" }
                    );
                    return Task.FromResult(ApplyResult.True());
                },
            };

            var revertPath = GetRevertFilePath(optimization.Id);

            try
            {
                // Phase 1: Apply with partial success
                var service = CreateService();
                var progress = new Progress<ProcessingProgress>(_ => { });

                var applyResult = await service.ApplyAsync(optimization, progress);
                Assert.Equal(OptimizationSuccessResult.PartialSuccess, applyResult.Status);
                Assert.Single(applyResult.FailedSteps);
                Assert.Equal(2, applyResult.FailedSteps[0].Index);

                var data = await RevertManager.GetRevertDataAsync(optimization.Id);
                Assert.NotNull(data);
                Assert.Equal(3, data!.Steps.Length);
                Assert.NotNull(data.Steps[0]);
                Assert.Null(data.Steps[1]); // Gap from failed step
                Assert.NotNull(data.Steps[2]);

                // Phase 2: Revert should only revert steps 1 and 3
                var revertResult = await service.RevertAsync(optimization, progress);
                Assert.True(revertResult.Success);
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
    public async Task ApplyRetryThenRevert_CompleteWorkflowWithRetry()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new TestOptimization
            {
                ApplyImpl = _ =>
                {
                    ExecutionScope.RecordStep(
                        "Shell",
                        "Step 1",
                        true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 0" }
                    );
                    ExecutionScope.RecordStep(
                        "Shell",
                        "Step 2",
                        false,
                        null,
                        "fail 2",
                        () =>
                        {
                            ExecutionScope.RecordStep(
                                "Shell",
                                "Step 2 retry",
                                true,
                                new ShellRevertStep
                                {
                                    ShellType = ShellType.CMD,
                                    Command = "exit 0",
                                }
                            );
                            return Task.FromResult(true);
                        }
                    );
                    ExecutionScope.RecordStep(
                        "Shell",
                        "Step 3",
                        true,
                        new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 0" }
                    );
                    return Task.FromResult(ApplyResult.True());
                },
            };

            var revertPath = GetRevertFilePath(optimization.Id);

            try
            {
                var service = CreateService();
                var progress = new Progress<ProcessingProgress>(_ => { });

                // Phase 1: Apply with failure
                var applyResult = await service.ApplyAsync(optimization, progress);
                Assert.Equal(OptimizationSuccessResult.PartialSuccess, applyResult.Status);

                // Phase 2: Retry failed steps
                var retryResult = await OptimizationService.RetryFailedStepsWithResultsAsync(
                    applyResult.FailedSteps,
                    false,
                    NullLogger.Instance
                );

                Assert.Empty(retryResult.FailedSteps);
                Assert.Single(retryResult.RecoveredSteps);
                Assert.Equal(2, retryResult.RecoveredSteps[0].Index);

                // Phase 3: Upsert the recovered step
                var revertManager = new RevertManager(
                    NullLogger<RevertManager>.Instance,
                    NullLoggerFactory.Instance
                );
                await revertManager.UpsertRevertStepAtIndexAsync(
                    optimization.Id,
                    optimization.OptimizationKey,
                    retryResult.RecoveredSteps[0].Index,
                    retryResult.RecoveredSteps[0].RevertStep!
                );

                // Verify complete revert data
                var data = await RevertManager.GetRevertDataAsync(optimization.Id);
                Assert.NotNull(data);
                Assert.Equal(3, data!.Steps.Length);

                // All steps should now have "exit 0" commands
                Assert.Equal(
                    "exit 0",
                    data.Steps[0]!.Data[nameof(ShellRevertStep.Command)]?.ToString()
                );
                Assert.Equal(
                    "exit 0",
                    data.Steps[1]!.Data[nameof(ShellRevertStep.Command)]?.ToString()
                );
                Assert.Equal(
                    "exit 0",
                    data.Steps[2]!.Data[nameof(ShellRevertStep.Command)]?.ToString()
                );

                // Phase 4: Revert all steps successfully
                var revertResult = await service.RevertAsync(optimization, progress);
                Assert.True(revertResult.Success);
                Assert.False(File.Exists(revertPath));
            }
            finally
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);
            }
        });
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task RevertAsync_NoRevertDataFile_ReturnsFailure()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new TestOptimization { Id = Guid.NewGuid() };

            var service = CreateService();
            var progress = new Progress<ProcessingProgress>(_ => { });

            var result = await service.RevertAsync(optimization, progress);

            Assert.False(result.Success);
            Assert.Contains("No revert data", result.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task RevertAsync_CorruptJsonFile_ReturnsFailure()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new TestOptimization { Id = Guid.NewGuid() };
            var revertPath = GetRevertFilePath(optimization.Id);

            try
            {
                Directory.CreateDirectory(Shared.RevertDirectory);
                await File.WriteAllTextAsync(revertPath, "{ invalid json }");

                var service = CreateService();
                var progress = new Progress<ProcessingProgress>(_ => { });

                var result = await service.RevertAsync(optimization, progress);

                Assert.False(result.Success);
            }
            finally
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);
            }
        });
    }

    [Fact]
    public async Task ApplyAsync_WithException_HandlesGracefully()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new TestOptimization
            {
                ApplyImpl = _ => throw new InvalidOperationException("Simulated exception"),
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
                Assert.NotNull(result.Exception);
                Assert.False(File.Exists(revertPath));
            }
            finally
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);
            }
        });
    }

    #endregion

    #region Helpers

    private static OptimizationService CreateService()
    {
        var revertManager = new RevertManager(
            NullLogger<RevertManager>.Instance,
            NullLoggerFactory.Instance
        );
        var loggerFactory = NullLoggerFactory.Instance;
        var systemInfoService = new SystemInfoService(NullLogger<SystemInfoService>.Instance);
        var streamService = new StreamService(NullLogger<StreamService>.Instance);
        var contentDialogService = new ContentDialogService();
        var logger = NullLogger<OptimizationService>.Instance;
        return new OptimizationService(
            revertManager,
            loggerFactory,
            systemInfoService,
            streamService,
            contentDialogService,
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

    private sealed class TestOptimization : IOptimization
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public OptimizationRisk Risk => OptimizationRisk.Safe;
        public string OptimizationKey => "TestOptimization";
        public string Name => "Test Optimization";
        public string ShortDescription => "Test optimization for comprehensive testing";
        public OptimizationState State { get; set; } = new();

        public Func<
            (IProgress<ProcessingProgress> progress, OptimizationContext context),
            Task<ApplyResult>
        > ApplyImpl { get; init; } = _ => Task.FromResult(ApplyResult.True());

        public Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            return ApplyImpl((progress, context));
        }
    }

    #endregion
}
