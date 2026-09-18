using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.History;
using optimizerDuck.Services.Optimization;
using optimizerDuck.Services.Revert;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.Test.TestDoubles;

namespace optimizerDuck.Test.Services;

/// <summary>
///     Pins the run lifecycle: the steps, outcome and revert data a run produces, what the
///     compensation guard reports, and that a run keeping no revert data writes nothing.
/// </summary>
public class OperationRunnerTests
{
    [Fact]
    public async Task Run_RecordsStatusStepsAndPersistedRevertData()
    {
        var subject = NewSubject();
        var runner = TestRunner.New(NewRevertManager());
        var revertPath = RevertPath(subject.Id);

        try
        {
            var result = await runner.RunAsync(
                new OperationRequest(
                    subject,
                    "Runner test optimization",
                    NullLogger.Instance,
                    RevertPersistence.Enabled,
                    (_, context) =>
                    {
                        context.Changes.Add(
                            "Test step",
                            "wrote a value",
                            true,
                            new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 0" }
                        );
                        context.Changes.AddSkip("Test step", "already set");
                        context.Changes.Add(
                            "Test step",
                            "refused value",
                            false,
                            null,
                            error: "refused"
                        );
                        return Task.FromResult(context.Changes.ToApplyResult());
                    }
                ),
                new RecordingProgress(),
                TestContext.Current.CancellationToken
            );

            Assert.Equal(OptimizationSuccessResult.PartialSuccess, result.Status);
            Assert.Single(result.FailedSteps);

            var data = await RevertManager.GetRevertDataAsync(subject.Id);
            Assert.NotNull(data);

            var steps = data!.Steps.Where(s => s != null).ToList();
            Assert.Single(steps);
            Assert.Equal(1, steps[0]!.Index);
            Assert.Equal("Shell", steps[0]!.Type);

            var record = ChangeRecordStore.TryRead(subject.Id);
            Assert.NotNull(record);
            Assert.Equal(3, record!.Steps.Count);
            Assert.Equal(subject.Key, record.OptimizationKey);
            Assert.Equal(1, record.ChangedCount);
            Assert.Equal(1, record.FailedCount);
        }
        finally
        {
            Cleanup(subject.Id);
            Assert.False(File.Exists(revertPath));
        }
    }

    [Fact]
    public async Task Run_ChangeWithoutCompensation_ReportsTheDefectForEveryRun()
    {
        foreach (var persistence in new[] { RevertPersistence.Enabled, RevertPersistence.Disabled })
        {
            var subject = NewSubject();
            var logger = new CapturingLogger();
            var runner = TestRunner.New(NewRevertManager());

            try
            {
                var result = await runner.RunAsync(
                    new OperationRequest(
                        subject,
                        "Runner test tool",
                        logger,
                        persistence,
                        (_, context) =>
                        {
                            context.Changes.Add("Test step", "wrote a value", true, null);
                            return Task.FromResult(context.Changes.ToApplyResult());
                        }
                    ),
                    new RecordingProgress(),
                    TestContext.Current.CancellationToken
                );

                Assert.Equal(OptimizationSuccessResult.Success, result.Status);
                Assert.Contains(logger.Warnings, w => w.Contains("carries no revert data"));
            }
            finally
            {
                Cleanup(subject.Id);
            }
        }
    }

    [Fact]
    public async Task ToolRun_RecordsStepsAndWritesNoRevertData()
    {
        var subject = NewToolSubject();
        var runner = TestRunner.New(NewRevertManager());

        try
        {
            var result = await runner.RunAsync(
                new OperationRequest(
                    subject,
                    "Runner test tool",
                    NullLogger.Instance,
                    RevertPersistence.Disabled,
                    (_, context) =>
                    {
                        context.Changes.AddSkip("Scheduled Task", "task already enabled");
                        context.Changes.AddIrreversible("Scheduled Task", "ran a task");
                        return Task.FromResult(context.Changes.ToApplyResult());
                    }
                ),
                new RecordingProgress(),
                TestContext.Current.CancellationToken
            );

            // An irreversible step modified the system, so this is a success, not nothing to do.
            Assert.Equal(OptimizationSuccessResult.Success, result.Status);

            var record = ChangeRecordStore.TryRead(subject.Id);
            Assert.NotNull(record);
            Assert.Equal(subject.Key, record!.OptimizationKey);
            Assert.Equal(2, record.Steps.Count);
            Assert.Equal(ChangeKind.Skip, record.Steps[0].Kind);
            Assert.Equal(ChangeKind.Irreversible, record.Steps[1].Kind);
            Assert.False(File.Exists(RevertPath(subject.Id)));
        }
        finally
        {
            Cleanup(subject.Id);
        }
    }

    [Fact]
    public async Task ToolRun_ChangeWithCompensation_PersistsNothing()
    {
        var subject = NewToolSubject();
        var runner = TestRunner.New(NewRevertManager());

        try
        {
            var result = await runner.RunAsync(
                new OperationRequest(
                    subject,
                    "Runner test tool",
                    NullLogger.Instance,
                    RevertPersistence.Disabled,
                    (_, context) =>
                    {
                        context.Changes.Add(
                            "Scheduled Task",
                            "disabled a task",
                            true,
                            new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 0" }
                        );
                        return Task.FromResult(context.Changes.ToApplyResult());
                    }
                ),
                new RecordingProgress(),
                TestContext.Current.CancellationToken
            );

            Assert.Equal(OptimizationSuccessResult.Success, result.Status);
            Assert.False(File.Exists(RevertPath(subject.Id)));

            var record = ChangeRecordStore.TryRead(subject.Id);
            Assert.NotNull(record);
            Assert.Equal(ChangeKind.Change, record!.Steps[0].Kind);
            Assert.Equal(1, record.ChangedCount);
        }
        finally
        {
            Cleanup(subject.Id);
        }
    }

    [Fact]
    public async Task ToolRun_RetryingAFailedStep_PersistsNoRevertData()
    {
        var subject = NewToolSubject();
        var runner = TestRunner.New(NewRevertManager());

        try
        {
            var result = await runner.RunAsync(
                new OperationRequest(
                    subject,
                    "Runner test tool",
                    NullLogger.Instance,
                    RevertPersistence.Disabled,
                    (_, context) =>
                    {
                        context.Changes.Add(
                            "Scheduled Task",
                            "disabled a task",
                            false,
                            null,
                            error: "refused",
                            retry: _ =>
                            {
                                context.Changes.Add(
                                    "Scheduled Task",
                                    "disabled a task",
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
                        return Task.FromResult(context.Changes.ToApplyResult());
                    }
                ),
                new RecordingProgress(),
                TestContext.Current.CancellationToken
            );

            Assert.Equal(OptimizationSuccessResult.Failed, result.Status);
            Assert.Single(result.FailedSteps);

            var retry = await OptimizationService.RetryFailedStepsWithResultsAsync(
                result.FailedSteps,
                reverseOrder: false,
                NullLogger.Instance,
                revertManager: null,
                optimizationId: subject.Id,
                optimizationKey: subject.Key
            );

            Assert.Single(retry.RecoveredSteps);
            Assert.Empty(retry.FailedSteps);
            Assert.False(File.Exists(RevertPath(subject.Id)));

            var record = ChangeRecordStore.TryRead(subject.Id);
            Assert.NotNull(record);
            Assert.Equal(1, record!.ChangedCount);
            Assert.Equal(0, record.FailedCount);
        }
        finally
        {
            Cleanup(subject.Id);
        }
    }

    [Fact]
    public async Task Run_WhenRevertDataCannotBePersisted_ReportsAFailedStepAndKeepsTheChange()
    {
        var subject = NewSubject();
        var runner = TestRunner.New(NewRevertManager());
        var revertPath = RevertPath(subject.Id);
        var key = $@"HKCU\Software\TestOptimizerDuckRunner\{Guid.NewGuid():N}";
        var written = new RegistryItem(key, "Value", 1);

        try
        {
            // A directory where the revert file belongs makes the atomic write fail.
            Directory.CreateDirectory(revertPath);

            var result = await runner.RunAsync(
                new OperationRequest(
                    subject,
                    "Runner test optimization",
                    NullLogger.Instance,
                    RevertPersistence.Enabled,
                    (_, context) =>
                    {
                        RegistryService.Write(context, written);
                        return Task.FromResult(context.Changes.ToApplyResult());
                    }
                ),
                new RecordingProgress(),
                TestContext.Current.CancellationToken
            );

            Assert.Equal(OptimizationSuccessResult.PartialSuccess, result.Status);

            var failed = Assert.Single(result.FailedSteps);
            Assert.False(string.IsNullOrWhiteSpace(failed.Error));
            Assert.Null(failed.Revert);

            // The run still did what it reported, and it reported that undo is unavailable.
            Assert.Equal((int?)1, RegistryService.Read<int>(written));
        }
        finally
        {
            RegistryService.DeleteSubKeyTree(
                new OpCall { Logger = NullLogger.Instance },
                new RegistryItem(key)
            );
            if (Directory.Exists(revertPath))
                Directory.Delete(revertPath);
            Cleanup(subject.Id);
        }
    }

    /// <summary>
    ///     Pins the outcome each recorded shape is reported as, for both revert modes.
    /// </summary>
    [Theory]
    [InlineData(RecordedShape.SkipOnly, OptimizationSuccessResult.NothingToDo)]
    [InlineData(RecordedShape.Change, OptimizationSuccessResult.Success)]
    [InlineData(RecordedShape.Irreversible, OptimizationSuccessResult.Success)]
    [InlineData(RecordedShape.ChangeAndIrreversible, OptimizationSuccessResult.Success)]
    [InlineData(RecordedShape.Failed, OptimizationSuccessResult.Failed)]
    [InlineData(RecordedShape.ChangeAndFailed, OptimizationSuccessResult.PartialSuccess)]
    [InlineData(RecordedShape.SkipThenProviderError, OptimizationSuccessResult.Failed)]
    [InlineData(
        RecordedShape.CompensatedFailureThenProviderError,
        OptimizationSuccessResult.PartialSuccess
    )]
    [InlineData(RecordedShape.SkipThenThrows, OptimizationSuccessResult.Failed)]
    [InlineData(RecordedShape.ChangeThenThrows, OptimizationSuccessResult.PartialSuccess)]
    public async Task Run_ClassifiesEveryRecordedShape(
        RecordedShape shape,
        OptimizationSuccessResult expected
    )
    {
        foreach (var persistence in new[] { RevertPersistence.Enabled, RevertPersistence.Disabled })
        {
            var subject = NewSubject();
            var runner = TestRunner.New(NewRevertManager());

            try
            {
                var result = await runner.RunAsync(
                    new OperationRequest(
                        subject,
                        "Runner test optimization",
                        NullLogger.Instance,
                        persistence,
                        (_, context) =>
                        {
                            Record(context.Changes, shape);
                            return Finish(context.Changes, shape);
                        }
                    ),
                    new RecordingProgress(),
                    TestContext.Current.CancellationToken
                );

                Assert.Equal(expected, result.Status);
            }
            finally
            {
                Cleanup(subject.Id);
            }
        }
    }

    /// <summary>A failure with no failed step reports the error the work gave.</summary>
    [Fact]
    public async Task Run_ProviderErrorWithoutAFailedStep_ReportsWhatWentWrong()
    {
        var subject = NewSubject();
        var runner = TestRunner.New(NewRevertManager());

        try
        {
            var result = await runner.RunAsync(
                new OperationRequest(
                    subject,
                    "Runner test tool",
                    NullLogger.Instance,
                    RevertPersistence.Disabled,
                    (_, _) => Task.FromResult(ApplyResult.False(ProviderErrorText))
                ),
                new RecordingProgress(),
                TestContext.Current.CancellationToken
            );

            Assert.Equal(OptimizationSuccessResult.Failed, result.Status);
            Assert.Empty(result.FailedSteps);
            Assert.Equal(ProviderErrorText, result.Message);
        }
        finally
        {
            Cleanup(subject.Id);
        }
    }

    /// <summary>A run that threw records a failed step named for the user, not for the log.</summary>
    [Fact]
    public async Task Run_Throws_RecordsTheFailureUnderTheDisplayName()
    {
        var subject = NewSubject();
        var runner = TestRunner.New(NewRevertManager());

        try
        {
            var result = await runner.RunAsync(
                new OperationRequest(
                    subject,
                    "Runner test optimization",
                    NullLogger.Instance,
                    RevertPersistence.Disabled,
                    (_, _) => throw new InvalidOperationException(ThrownText)
                ),
                new RecordingProgress(),
                TestContext.Current.CancellationToken
            );

            var failed = Assert.Single(result.FailedSteps);
            Assert.Equal("Runner test optimization", failed.Name);
            Assert.Equal(subject.Key, failed.Description);
        }
        finally
        {
            Cleanup(subject.Id);
        }
    }

    private static OperationSubject NewSubject() =>
        new(Guid.NewGuid(), "RunnerTestSubject", "Runner test subject");

    /// <summary>A subject owned by this suite, so no two suites share a record file.</summary>
    private static OperationSubject NewToolSubject() =>
        new(Guid.NewGuid(), "RunnerTestTool", "Runner test tool");

    private static string RevertPath(Guid id) => Path.Combine(Shared.RevertDirectory, id + ".json");

    private static RevertManager NewRevertManager() =>
        new(
            NullLogger<RevertManager>.Instance,
            TestShell.New(),
            new PowerPlanService(NullLogger<PowerPlanService>.Instance),
            TimeProvider.System
        );

    private static void Cleanup(Guid id)
    {
        foreach (
            var path in new[]
            {
                ChangeRecordStore.PathFor(id),
                RevertPath(id),
                RevertPath(id) + ".tmp",
            }
        )
            if (File.Exists(path))
                File.Delete(path);
    }

    /// <summary>Records the steps one shape is made of.</summary>
    private static void Record(ChangeSet changes, RecordedShape shape)
    {
        switch (shape)
        {
            case RecordedShape.SkipOnly:
            case RecordedShape.SkipThenProviderError:
            case RecordedShape.SkipThenThrows:
                changes.AddSkip("Test step", "already set");
                break;
            case RecordedShape.Change:
            case RecordedShape.ChangeThenThrows:
                changes.Add("Test step", "wrote a value", true, TestCompensation());
                break;
            case RecordedShape.CompensatedFailureThenProviderError:
                changes.Add(
                    "Test step",
                    "wrote a value, then a device refused",
                    false,
                    TestCompensation(),
                    "a device refused the write"
                );
                break;
            case RecordedShape.Irreversible:
                changes.AddIrreversible("Test step", "ran a task");
                break;
            case RecordedShape.ChangeAndIrreversible:
                changes.Add("Test step", "wrote a value", true, TestCompensation());
                changes.AddIrreversible("Test step", "ran a task");
                break;
            case RecordedShape.Failed:
                changes.Add("Test step", "wrote a value", false, null, error: "refused");
                break;
            case RecordedShape.ChangeAndFailed:
                changes.Add("Test step", "wrote a value", true, TestCompensation());
                changes.Add("Test step", "wrote a value", false, null, error: "refused");
                break;
        }
    }

    private static ShellRevertStep TestCompensation() =>
        new() { ShellType = ShellType.CMD, Command = "exit 0" };

    /// <summary>How a run ends once the steps of its shape are recorded.</summary>
    private static Task<ApplyResult> Finish(ChangeSet changes, RecordedShape shape) =>
        shape switch
        {
            RecordedShape.SkipThenProviderError
            or RecordedShape.CompensatedFailureThenProviderError => Task.FromResult(
                ApplyResult.False(ProviderErrorText)
            ),
            RecordedShape.SkipThenThrows or RecordedShape.ChangeThenThrows =>
                throw new InvalidOperationException(ThrownText),
            _ => Task.FromResult(changes.ToApplyResult()),
        };

    /// <summary>What a work reports when it did not succeed and recorded no failed step.</summary>
    private const string ProviderErrorText = "the entry is no longer on this machine";

    private const string ThrownText = "simulated";

    /// <summary>The recorded shapes a run can end with, which decide its reported outcome.</summary>
    public enum RecordedShape
    {
        SkipOnly,
        Change,
        Irreversible,
        ChangeAndIrreversible,
        Failed,
        ChangeAndFailed,
        SkipThenProviderError,
        CompensatedFailureThenProviderError,
        SkipThenThrows,
        ChangeThenThrows,
    }

    private sealed class RecordingProgress : IProgress<ProcessingProgress>
    {
        public void Report(ProcessingProgress value) { }
    }

    private sealed class CapturingLogger : ILogger
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
}
