using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Revert;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Managers;

namespace optimizerDuck.Test.Services.Managers;

public class RevertManagerTests
{
    [Fact]
    public async Task IsAppliedAsync_And_GetRevertDataAsync_HandleMissingFile()
    {
        var id = Guid.NewGuid();

        var isApplied = await RevertManager.IsAppliedAsync(id);
        var data = await RevertManager.GetRevertDataAsync(id);

        Assert.False(isApplied);
        Assert.Null(data);
    }

    [Fact]
    public async Task GetRevertDataAsync_ReadsValidPayload()
    {
        var id = Guid.NewGuid();
        var path = Path.Combine(Shared.RevertDirectory, id + ".json");
        var cancellationToken = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Shared.RevertDirectory);

        var payload = new RevertData
        {
            OptimizationId = id,
            OptimizationName = "TestOptimization",
            AppliedAt = DateTime.UtcNow,
            Steps = Array.Empty<RevertStepData?>()
        };

        try
        {
            var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
            await File.WriteAllTextAsync(path, json, cancellationToken);

            var data = await RevertManager.GetRevertDataAsync(id);

            Assert.NotNull(data);
            Assert.Equal(id, data!.OptimizationId);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task RevertAsync_WithInvalidJson_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        var path = Path.Combine(Shared.RevertDirectory, id + ".json");
        var cancellationToken = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Shared.RevertDirectory);

        try
        {
            await File.WriteAllTextAsync(path, "{ invalid json }", cancellationToken);

            var manager = new RevertManager(
                NullLogger<RevertManager>.Instance,
                NullLoggerFactory.Instance
            );
            var op = new MockOptimization(id);
            var result = await manager.RevertAsync(op);

            Assert.False(result.Success);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task RevertAsync_WithPartialStepFailures_ReturnsFailure_And_DeletesFile()
    {
        var id = Guid.NewGuid();
        var path = Path.Combine(Shared.RevertDirectory, id + ".json");
        var cancellationToken = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Shared.RevertDirectory);

        var payload = new RevertData
        {
            OptimizationId = id,
            OptimizationName = "TestOptimization",
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
                        ShellType = ShellType.CMD,
                        Command = "exit 1",
                    }.ToData(),
                },
            },
        };

        try
        {
            var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
            await File.WriteAllTextAsync(path, json, cancellationToken);

            var manager = new RevertManager(
                NullLogger<RevertManager>.Instance,
                NullLoggerFactory.Instance
            );
            var result = await manager.RevertAsync(new MockOptimization(id));

            Assert.False(result.Success);
            Assert.False(File.Exists(path));
            var failedStep = Assert.Single(result.FailedSteps);
            Assert.NotNull(failedStep.RetryAction);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task RevertAsync_WithAllStepFailures_LeavesFileForAnotherAttempt()
    {
        var id = Guid.NewGuid();
        var path = Path.Combine(Shared.RevertDirectory, id + ".json");
        var cancellationToken = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Shared.RevertDirectory);

        var payload = new RevertData
        {
            OptimizationId = id,
            OptimizationName = "TestOptimization",
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
                        Command = "exit 1",
                    }.ToData(),
                },
            },
        };

        try
        {
            var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
            await File.WriteAllTextAsync(path, json, cancellationToken);

            var manager = new RevertManager(
                NullLogger<RevertManager>.Instance,
                NullLoggerFactory.Instance
            );
            var result = await manager.RevertAsync(new MockOptimization(id));

            Assert.False(result.Success);
            Assert.True(result.AllStepsFailed);
            Assert.True(File.Exists(path));
            var failedStep = Assert.Single(result.FailedSteps);
            Assert.NotNull(failedStep.RetryAction);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task RevertAsync_WithPartialFailures_RetryActionSucceedsAfterRevertDataIsDeleted()
    {
        var id = Guid.NewGuid();
        var path = Path.Combine(Shared.RevertDirectory, id + ".json");
        var cancellationToken = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Shared.RevertDirectory);

        var payload = new RevertData
        {
            OptimizationId = id,
            OptimizationName = "TestOptimization",
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
                    Type = RetryableTestRevertStep.StepType,
                    Data = new RetryableTestRevertStep
                    {
                        StepId = Guid.NewGuid().ToString("N"),
                        RemainingFailures = 1,
                    }.ToData(),
                },
            },
        };

        try
        {
            var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
            await File.WriteAllTextAsync(path, json, cancellationToken);

            var manager = new RevertManager(
                NullLogger<RevertManager>.Instance,
                NullLoggerFactory.Instance
            );
            var result = await manager.RevertAsync(new MockOptimization(id));

            Assert.False(result.Success);
            Assert.False(File.Exists(path));

            var failedStep = Assert.Single(result.FailedSteps);
            Assert.NotNull(failedStep.RetryAction);
            Assert.True(await failedStep.RetryAction!());
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}

public class MockOptimization(Guid id) : IOptimization
{
    public Guid Id { get; } = id;
    public OptimizationRisk Risk => OptimizationRisk.Safe;
    public string OptimizationKey => "TestOptimization";
    public string Name => "TestOptimization";
    public string ShortDescription => "Mock description";
    public OptimizationState State { get; set; } = new();

    public Task<ApplyResult> ApplyAsync(
        IProgress<ProcessingProgress> progress,
        OptimizationContext context
    )
    {
        return Task.FromResult(ApplyResult.True());
    }
}

public class RetryableTestRevertStep : IRevertStep
{
    public const string StepType = "RetryableTest";

    public string StepId { get; set; } = Guid.NewGuid().ToString("N");

    public int RemainingFailures { get; set; }

    public string Type => StepType;

    public string Description => $"Retryable test step {StepId}";

    public Task<bool> ExecuteAsync()
    {
        if (RemainingFailures > 0)
        {
            RemainingFailures--;
            throw new InvalidOperationException("planned test failure");
        }

        return Task.FromResult(true);
    }

    public JObject ToData()
    {
        return new JObject
        {
            [nameof(StepId)] = StepId,
            [nameof(RemainingFailures)] = RemainingFailures,
        };
    }

    public static RetryableTestRevertStep FromData(JToken data)
    {
        return new RetryableTestRevertStep
        {
            StepId = data[nameof(StepId)]?.ToString() ?? Guid.NewGuid().ToString("N"),
            RemainingFailures = data[nameof(RemainingFailures)]?.Value<int>() ?? 0,
        };
    }
}
