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
using optimizerDuck.Services.Optimization;
using optimizerDuck.Services.Revert;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;
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
    public async Task IsAppliedAsync_WithInvalidJson_CountsAsApplied()
    {
        // Deliberate reversal: a file that exists but cannot be parsed still means the item was
        // applied once. Reporting it as untouched invited a fresh apply, which then wrote over
        // the only copy of the backup. The payload is parked by the next save instead, which
        // RevertIntegrityTests covers.
        var id = Guid.NewGuid();
        var path = Path.Combine(Shared.RevertDirectory, id + ".json");
        var cancellationToken = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Shared.RevertDirectory);

        try
        {
            await File.WriteAllTextAsync(path, "{ invalid json }", cancellationToken);

            var isApplied = await RevertManager.IsAppliedAsync(id);

            Assert.True(isApplied);
        }
        finally
        {
            foreach (var file in Directory.GetFiles(Shared.RevertDirectory, id + ".json*"))
                File.Delete(file);
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
                new PowerPlanService(NullLogger<PowerPlanService>.Instance),
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
                new PowerPlanService(NullLogger<PowerPlanService>.Instance),
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
                new PowerPlanService(NullLogger<PowerPlanService>.Instance),
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
                new PowerPlanService(NullLogger<PowerPlanService>.Instance),
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
            new PowerPlanService(NullLogger<PowerPlanService>.Instance),
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
            new PowerPlanService(NullLogger<PowerPlanService>.Instance),
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
                new PowerPlanService(NullLogger<PowerPlanService>.Instance),
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

/// <summary>
///     Pins the guard that a step recorded as a change carries the data needed to undo it.
///     The writer filters entries without compensation, so the warning is the only signal
///     that a provider forgot one.
/// </summary>
public class RevertManagerChangeGuardTests
{
    private sealed class CapturingLogger : ILogger<RevertManager>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (logLevel == LogLevel.Warning)
                Warnings.Add(formatter(state, exception));
        }
    }

    [Fact]
    public async Task SaveRevertDataAsync_ChangeWithoutCompensation_WarnsAndWritesNoFile()
    {
        var logger = new CapturingLogger();
        var manager = new RevertManager(
            logger,
            TestShell.New(),
            new PowerPlanService(NullLogger<PowerPlanService>.Instance),
            TimeProvider.System
        );
        var id = Guid.NewGuid();
        var path = Path.Combine(Shared.RevertDirectory, id + ".json");
        var changes = new ChangeSet();
        changes.Add("Registry", "wrote a value", true, null);

        try
        {
            await manager.SaveRevertDataAsync(
                changes,
                id,
                "GuardTestOptimization",
                TestContext.Current.CancellationToken
            );

            Assert.Contains(logger.Warnings, w => w.Contains("carries no revert data"));
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}

/// <summary>
///     Pins how revert data survives being unreadable: the bytes are kept, the item still counts
///     as applied, and a revert says which file it could not read instead of claiming none.
/// </summary>
public class RevertIntegrityTests
{
    private static RevertManager NewManager() =>
        new(
            NullLogger<RevertManager>.Instance,
            TestShell.New(),
            new PowerPlanService(NullLogger<PowerPlanService>.Instance),
            TimeProvider.System
        );

    private static void Cleanup(Guid id)
    {
        foreach (var file in Directory.GetFiles(Shared.RevertDirectory, id + ".json*"))
            File.Delete(file);
    }

    [Fact]
    public async Task SaveRevertDataAsync_UnreadableFile_IsParkedAndKeptByteForByte()
    {
        var id = Guid.NewGuid();
        var path = Path.Combine(Shared.RevertDirectory, id + ".json");
        var cancellationToken = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Shared.RevertDirectory);
        const string damaged = "{ this is not valid json }";

        try
        {
            await File.WriteAllTextAsync(path, damaged, cancellationToken);

            var changes = new ChangeSet();
            changes.Add("Registry", "wrote a value", true, new MockRevertStep());

            await NewManager().SaveRevertDataAsync(changes, id, "IntegrityTest", cancellationToken);

            var parked = Directory.GetFiles(Shared.RevertDirectory, id + ".json.unreadable-*");
            Assert.Single(parked);
            Assert.Equal(damaged, await File.ReadAllTextAsync(parked[0], cancellationToken));
            Assert.True(File.Exists(path));
            Assert.NotEqual(damaged, await File.ReadAllTextAsync(path, cancellationToken));
        }
        finally
        {
            Cleanup(id);
        }
    }

    [Fact]
    public async Task SaveRevertDataAsync_SchemaFromAFutureBuild_IsParked()
    {
        var id = Guid.NewGuid();
        var path = Path.Combine(Shared.RevertDirectory, id + ".json");
        var cancellationToken = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Shared.RevertDirectory);

        try
        {
            var future = JsonConvert.SerializeObject(
                new RevertData
                {
                    SchemaVersion = 99,
                    OptimizationId = id,
                    OptimizationName = "Future",
                    AppliedAt = DateTime.UtcNow,
                    Steps = Array.Empty<RevertStepData?>(),
                }
            );
            await File.WriteAllTextAsync(path, future, cancellationToken);

            var changes = new ChangeSet();
            changes.Add("Registry", "wrote a value", true, new MockRevertStep());

            await NewManager().SaveRevertDataAsync(changes, id, "IntegrityTest", cancellationToken);

            var parked = Directory.GetFiles(Shared.RevertDirectory, id + ".json.unreadable-*");
            Assert.Single(parked);
            Assert.Contains("SchemaVersion", await File.ReadAllTextAsync(parked[0], cancellationToken));
        }
        finally
        {
            Cleanup(id);
        }
    }

    [Fact]
    public async Task IsAppliedAsync_CountsUnreadableAndParkedData()
    {
        var id = Guid.NewGuid();
        var path = Path.Combine(Shared.RevertDirectory, id + ".json");
        var cancellationToken = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Shared.RevertDirectory);

        try
        {
            Assert.False(await RevertManager.IsAppliedAsync(id));

            await File.WriteAllTextAsync(path, "{ broken }", cancellationToken);
            Assert.True(await RevertManager.IsAppliedAsync(id));

            var parked = path + ".unreadable-20260101000000";
            File.Move(path, parked);
            Assert.True(await RevertManager.IsAppliedAsync(id));

            File.Delete(parked);
            Assert.False(await RevertManager.IsAppliedAsync(id));
        }
        finally
        {
            Cleanup(id);
        }
    }

    [Fact]
    public async Task RevertAsync_UnreadableData_NamesTheFileAndKeepsIt()
    {
        var id = Guid.NewGuid();
        var path = Path.Combine(Shared.RevertDirectory, id + ".json");
        var cancellationToken = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Shared.RevertDirectory);

        try
        {
            await File.WriteAllTextAsync(path, "{ broken }", cancellationToken);

            var result = await NewManager()
                .RevertAsync(new MockOptimization(id), cancellationToken: cancellationToken);

            Assert.False(result.Success);
            // The path is substituted into the message, so this holds in every language.
            Assert.Contains(path, result.Message);
            Assert.True(File.Exists(path));
        }
        finally
        {
            Cleanup(id);
        }
    }

    [Fact]
    public async Task SaveRevertDataAsync_FailedChangeWithCompensation_IsStillPersisted()
    {
        // A step that was refused after it had already changed something still needs its
        // previous state recorded, so the writer no longer requires a success flag.
        var id = Guid.NewGuid();
        var changes = new ChangeSet();
        changes.Add(
            "Registry",
            "wrote a value, then the flag write was refused",
            false,
            new MockRevertStep(),
            "refused"
        );

        try
        {
            await NewManager()
                .SaveRevertDataAsync(
                    changes,
                    id,
                    "FailedStepTest",
                    TestContext.Current.CancellationToken
                );

            var data = await RevertManager.GetRevertDataAsync(id);
            Assert.NotNull(data);
            Assert.Single(data!.Steps, s => s != null);
        }
        finally
        {
            Cleanup(id);
        }
    }

    [Fact]
    public async Task RetryFailedSteps_WhenTheCompensationCannotBePersisted_ReportsTheStepAsStillFailed()
    {
        var id = Guid.NewGuid();
        var failed = new Change
        {
            Index = 1,
            Name = "Step",
            Description = "Step",
            Ok = false,
            Retry = call =>
            {
                call.Changes.Add("Registry", "wrote a value", true, new MockRevertStep());
                return Task.FromResult(OpResult.Success());
            },
        };

        // Hold the file lock so the append cannot take it: the retry ran, but its compensation
        // never reached the file, which must not be reported as a clean recovery.
        var gate = RevertManager.FileLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            var manager = new RevertManager(
                NullLogger<RevertManager>.Instance,
                TestShell.New(),
                new PowerPlanService(NullLogger<PowerPlanService>.Instance),
                TimeProvider.System,
                TimeSpan.FromMilliseconds(1)
            );

            var result = await OptimizationService.RetryFailedStepsWithResultsAsync(
                [failed],
                false,
                NullLogger.Instance,
                manager,
                id,
                "RetryTest"
            );

            Assert.Empty(result.RecoveredSteps);
            var stillFailed = Assert.Single(result.FailedSteps);
            Assert.False(stillFailed.Ok);
            Assert.Contains("lock", stillFailed.Error ?? string.Empty);
            Assert.False(File.Exists(Path.Combine(Shared.RevertDirectory, id + ".json")));
        }
        finally
        {
            gate.Release();
            Cleanup(id);
        }
    }

    [Fact]
    public async Task RevertAsync_KnownStepTypeWithUnreadablePayload_FailsLoudlyAndKeepsTheFile()
    {
        var id = Guid.NewGuid();
        var path = Path.Combine(Shared.RevertDirectory, id + ".json");
        var cancellationToken = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Shared.RevertDirectory);

        try
        {
            // A registered type whose payload is missing its required fields: that entry must stay
            // loadable, so the rest of the file still reverts and the failure names the type.
            await File.WriteAllTextAsync(
                path,
                JsonConvert.SerializeObject(
                    new RevertData
                    {
                        SchemaVersion = 1,
                        OptimizationId = id,
                        OptimizationName = "UnreadablePayloadTest",
                        AppliedAt = DateTime.UtcNow,
                        Steps = new RevertStepData?[]
                        {
                            new()
                            {
                                Index = 1,
                                Type = "Registry",
                                Data = new JObject(),
                            },
                            new()
                            {
                                Index = 2,
                                Type = "Shell",
                                Data = new JObject
                                {
                                    ["ShellType"] = "CMD",
                                    ["Command"] = "exit 0",
                                },
                            },
                        },
                    }
                ),
                cancellationToken
            );

            var result = await NewManager()
                .RevertAsync(new MockOptimization(id), cancellationToken: cancellationToken);

            Assert.False(result.Success);
            var failed = Assert.Single(result.FailedSteps);
            Assert.Equal("Registry", failed.Name);
            Assert.Contains("Registry", failed.Error ?? string.Empty);
            Assert.True(File.Exists(path));
        }
        finally
        {
            Cleanup(id);
        }
    }

    [Fact]
    public async Task RetryFailedSteps_PersistsEveryRecoveredCompensation()
    {
        var id = Guid.NewGuid();
        var failed = new Change
        {
            Index = 1,
            Name = "Step",
            Description = "Step",
            Ok = false,
            Retry = call =>
            {
                call.Changes.Add("Registry", "first", true, new MockRevertStep());
                call.Changes.Add("Registry", "second", true, new MockRevertStep());
                return Task.FromResult(OpResult.Success());
            },
        };

        try
        {
            var result = await OptimizationService.RetryFailedStepsWithResultsAsync(
                [failed],
                false,
                NullLogger.Instance,
                NewManager(),
                id,
                "RetryTest"
            );

            Assert.Empty(result.FailedSteps);
            var data = await RevertManager.GetRevertDataAsync(id);
            Assert.NotNull(data);
            Assert.Equal(2, data!.Steps.Count(s => s != null));
        }
        finally
        {
            Cleanup(id);
        }
    }

    [Fact]
    public async Task RevertAsync_CleanupCannotTakeTheLock_ReturnsAResultWithAWarning()
    {
        var id = Guid.NewGuid();
        var path = Path.Combine(Shared.RevertDirectory, id + ".json");
        var cancellationToken = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Shared.RevertDirectory);

        // Hold the file lock so the prune after the revert cannot take it. One step succeeds and
        // one fails, which is the case that reaches the prune instead of deleting the file.
        var gate = RevertManager.FileLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await File.WriteAllTextAsync(
                path,
                JsonConvert.SerializeObject(
                    new RevertData
                    {
                        SchemaVersion = 1,
                        OptimizationId = id,
                        OptimizationName = "LockTest",
                        AppliedAt = DateTime.UtcNow,
                        Steps = new RevertStepData?[]
                        {
                            new()
                            {
                                Index = 1,
                                Type = "Shell",
                                Data = new JObject
                                {
                                    ["ShellType"] = "CMD",
                                    ["Command"] = "exit 0",
                                },
                            },
                            new()
                            {
                                Index = 2,
                                Type = "Shell",
                                Data = new JObject
                                {
                                    ["ShellType"] = "CMD",
                                    ["Command"] = "exit 1",
                                },
                            },
                        },
                    }
                ),
                cancellationToken
            );

            var manager = new RevertManager(
                NullLogger<RevertManager>.Instance,
                TestShell.New(),
                new PowerPlanService(NullLogger<PowerPlanService>.Instance),
                TimeProvider.System,
                TimeSpan.FromMilliseconds(150)
            );

            var result = await manager.RevertAsync(
                new MockOptimization(id),
                cancellationToken: cancellationToken
            );

            Assert.True(result.CleanupFailed);
            Assert.False(result.Success);
            Assert.True(File.Exists(path));
        }
        finally
        {
            gate.Release();
            Cleanup(id);
        }
    }
}

public class RetryableTestRevertStep : IRevertStep
{
    public const string StepType = "RetryableTest";

    public string StepId { get; set; } = Guid.NewGuid().ToString("N");

    public int RemainingFailures { get; set; }

    public string Type => StepType;

    public string Description => $"Retryable test step {StepId}";

    public Task<bool> ExecuteAsync(RevertContext _, ILogger logger)
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
