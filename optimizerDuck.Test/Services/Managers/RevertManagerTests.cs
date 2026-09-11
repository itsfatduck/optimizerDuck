using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Revert;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Optimization.Providers;
using optimizerDuck.Services.Revert;
using optimizerDuck.Test.TestDoubles;

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
            Steps = Array.Empty<RevertStepData?>(),
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
    public async Task IsAppliedAsync_WithInvalidJson_ReturnsFalse()
    {
        var id = Guid.NewGuid();
        var path = Path.Combine(Shared.RevertDirectory, id + ".json");
        var cancellationToken = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Shared.RevertDirectory);

        try
        {
            await File.WriteAllTextAsync(path, "{ invalid json }", cancellationToken);

            var isApplied = await RevertManager.IsAppliedAsync(id);

            Assert.False(isApplied);
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
                TestShell.New(),
                TimeProvider.System
            );
            var op = new MockOptimization(id);
            var result = await manager.RevertAsync(
                op,
                cancellationToken: TestContext.Current.CancellationToken
            );

            Assert.False(result.Success);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task RevertAsync_WithPartialStepFailures_ReturnsFailure_And_KeepsRemainingFailedStepsInFile()
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
                TestShell.New(),
                TimeProvider.System
            );
            var result = await manager.RevertAsync(
                new MockOptimization(id),
                cancellationToken: TestContext.Current.CancellationToken
            );

            Assert.False(result.Success);
            Assert.True(File.Exists(path));
            var failedStep = Assert.Single(result.FailedSteps);
            Assert.Equal(2, failedStep.Index);
            Assert.NotNull(failedStep.Retry);

            var updatedData = await RevertManager.GetRevertDataAsync(id);
            Assert.NotNull(updatedData);
            Assert.Null(updatedData!.Steps[0]);
            Assert.NotNull(updatedData.Steps[1]);
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
                TestShell.New(),
                TimeProvider.System
            );
            var result = await manager.RevertAsync(
                new MockOptimization(id),
                cancellationToken: TestContext.Current.CancellationToken
            );

            Assert.False(result.Success);
            Assert.True(result.AllStepsFailed);
            Assert.True(File.Exists(path));
            var failedStep = Assert.Single(result.FailedSteps);
            Assert.NotNull(failedStep.Retry);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task RevertAsync_WithPartialFailures_RetryingFailedStep_RemovesStepFromRevertData()
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
                TestShell.New(),
                TimeProvider.System
            );
            var result = await manager.RevertAsync(
                new MockOptimization(id),
                cancellationToken: TestContext.Current.CancellationToken
            );

            Assert.False(result.Success);
            Assert.True(File.Exists(path));

            var failedStep = Assert.Single(result.FailedSteps);
            Assert.NotNull(failedStep.Retry);
            Assert.True((await failedStep.Retry!(new OpCall { Logger = NullLogger.Instance })).Ok);

            await manager.RemoveRevertStepAtIndexAsync(id, "TestOptimization", failedStep.Index);
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveRevertDataAsync_SecondSave_AppendsWithoutLosingFirstSave()
    {
        var id = Guid.NewGuid();
        var path = Path.Combine(Shared.RevertDirectory, id + ".json");
        var manager = new RevertManager(
            NullLogger<RevertManager>.Instance,
            TestShell.New(),
            TimeProvider.System
        );

        var first = new ChangeSet();
        first.Add("Test", "First", true, new RetryableTestRevertStep { StepId = "first" });

        var second = new ChangeSet();
        second.Add("Test", "Second", true, new RetryableTestRevertStep { StepId = "second" });

        try
        {
            await manager.SaveRevertDataAsync(
                first,
                id,
                "Test",
                TestContext.Current.CancellationToken
            );
            await manager.SaveRevertDataAsync(
                second,
                id,
                "Test",
                TestContext.Current.CancellationToken
            );

            var data = await RevertManager.GetRevertDataAsync(id);
            Assert.NotNull(data);
            Assert.Equal(2, data!.Steps.Length);
            string?[] ids = data
                .Steps.Where(s => s != null)
                .Select(s => s!.Data[nameof(RetryableTestRevertStep.StepId)]?.ToString())
                .ToArray();
            Assert.Equal(new string?[] { "first", "second" }, ids);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveRevertDataAsync_DuplicatePayload_BothEntriesPersist()
    {
        // Regression: payload-based dedupe once dropped the second of two
        // identical executions, losing revert coverage.
        var id = Guid.NewGuid();
        var path = Path.Combine(Shared.RevertDirectory, id + ".json");
        var manager = new RevertManager(
            NullLogger<RevertManager>.Instance,
            TestShell.New(),
            TimeProvider.System
        );

        var changes = new ChangeSet();
        changes.Add("One", "One", true, new RetryableTestRevertStep { StepId = "same" });
        changes.Add("Two", "Two", true, new RetryableTestRevertStep { StepId = "same" });

        try
        {
            await manager.SaveRevertDataAsync(
                changes,
                id,
                "Test",
                TestContext.Current.CancellationToken
            );

            var data = await RevertManager.GetRevertDataAsync(id);
            Assert.NotNull(data);
            Assert.Equal(2, data!.Steps.Length);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task RevertAsync_UnknownStepType_FailsLoudlyAndKeepsFile()
    {
        var id = Guid.NewGuid();
        var path = Path.Combine(Shared.RevertDirectory, id + ".json");
        Directory.CreateDirectory(Shared.RevertDirectory);
        var cancellationToken = TestContext.Current.CancellationToken;

        var payload = new RevertData
        {
            OptimizationId = id,
            OptimizationName = "TestOptimization",
            AppliedAt = DateTime.UtcNow,
            Steps =
            [
                new RevertStepData
                {
                    Index = 1,
                    Type = "NoSuchStepType",
                    Data = new JObject { ["foo"] = "bar" },
                },
            ],
        };

        try
        {
            await File.WriteAllTextAsync(
                path,
                JsonConvert.SerializeObject(payload),
                cancellationToken
            );

            var manager = new RevertManager(
                NullLogger<RevertManager>.Instance,
                TestShell.New(),
                TimeProvider.System
            );
            var result = await manager.RevertAsync(
                new MockOptimization(id),
                cancellationToken: TestContext.Current.CancellationToken
            );

            Assert.False(result.Success);
            Assert.Single(result.FailedSteps);
            Assert.Contains("NoSuchStepType", result.FailedSteps[0].Error);
            // The unloadable entry is kept for a future version, not deleted.
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void RemoveOrphanedTempFiles_DeletesTmpKeepsJson()
    {
        Directory.CreateDirectory(Shared.RevertDirectory);
        var tmp = Path.Combine(Shared.RevertDirectory, Guid.NewGuid() + ".json.tmp");
        var json = Path.Combine(Shared.RevertDirectory, Guid.NewGuid() + ".json");

        try
        {
            File.WriteAllText(tmp, "{}");
            File.WriteAllText(json, "{}");

            RevertManager.RemoveOrphanedTempFiles(NullLogger.Instance);

            Assert.False(File.Exists(tmp));
            Assert.True(File.Exists(json));
        }
        finally
        {
            if (File.Exists(tmp))
                File.Delete(tmp);
            if (File.Exists(json))
                File.Delete(json);
        }
    }

    [Fact]
    public async Task LoadAsync_PathTraversalSiblingDirectory_ReturnsNull()
    {
        var siblingDir = Shared.RevertDirectory.TrimEnd(Path.DirectorySeparatorChar) + "Sibling";
        Directory.CreateDirectory(siblingDir);
        var siblingFile = Path.Combine(siblingDir, $"{Guid.NewGuid()}.json");
        var cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            var payload = new RevertData
            {
                OptimizationId = Guid.NewGuid(),
                OptimizationName = "Test",
                AppliedAt = DateTime.UtcNow,
                Steps = [],
            };
            await File.WriteAllTextAsync(
                siblingFile,
                JsonConvert.SerializeObject(payload),
                cancellationToken
            );

            var loadMethod = typeof(RevertManager).GetMethod(
                "LoadAsync",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic
            );
            Assert.NotNull(loadMethod);

            var task = (Task<RevertData?>)loadMethod!.Invoke(null, [siblingFile, null])!;
            var result = await task;

            Assert.Null(result);
        }
        finally
        {
            if (File.Exists(siblingFile))
                File.Delete(siblingFile);
            if (Directory.Exists(siblingDir))
                Directory.Delete(siblingDir);
        }
    }

    [Fact]
    public void BuildStepRegistry_DoesNotThrowOnCorruptedStepOrTypesWithoutDefaultConstructor()
    {
        var buildMethod = typeof(RevertManager).GetMethod(
            "BuildStepRegistry",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic
        );
        Assert.NotNull(buildMethod);

        var registry =
            (Dictionary<string, Func<JObject, IRevertStep>>)buildMethod!.Invoke(null, null)!;
        Assert.NotNull(registry);
        Assert.True(registry.ContainsKey("Registry"));
        Assert.True(registry.ContainsKey("Service"));
        Assert.True(registry.ContainsKey("ScheduledTask"));
        Assert.True(registry.ContainsKey("Shell"));
    }
}

public class MockOptimization(Guid id) : StubOptimization
{
    public override Guid Id => id;
    public override string OptimizationKey => "TestOptimization";
    public override string Name => "TestOptimization";
    public override string ShortDescription => "Mock description";

    public override Task<ApplyResult> ApplyAsync(
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

    public Task<bool> ExecuteAsync(ShellService _, ILogger logger)
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
