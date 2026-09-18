using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.ScheduledTask;
using optimizerDuck.Domain.Optimizations.Models.StartupManager;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.History;
using optimizerDuck.Services.Revert;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.Services.UI;
using optimizerDuck.Test.TestDoubles;

namespace optimizerDuck.Test.Services.UI;

/// <summary>
///     Pins what a startup manager task toggle does through the run lifecycle: the provider's step
///     is recorded under the startup manager's subject, no revert data is persisted, and an entry
///     Windows no longer has is reported as a failure naming it.
/// </summary>
/// <remarks>
///     These drive the work the page's delegate performs, not the page's wiring of it.
/// </remarks>
public class StartupManagerTaskRunTests
{
    [Fact]
    public async Task TogglingALiveTask_RecordsTheChangeUnderItsOwnSubject()
    {
        var name = $"optimizerDuck_Test_{Guid.NewGuid():N}";

        try
        {
            Assert.True(Register(name).Ok);

            var result = await RunToggleAsync(Task(name), enable: false);

            Assert.Equal(OptimizationSuccessResult.Success, result.Status);
            Assert.Empty(result.FailedSteps);

            var record = ChangeRecordStore.TryRead(ToolSubjects.StartupManagerTasks.Id);
            Assert.NotNull(record);
            Assert.Equal(ToolSubjects.StartupManagerTasks.Key, record!.OptimizationKey);
            Assert.Equal(ChangeKind.Change, Assert.Single(record.Steps).Kind);

            // A tool run keeps no revert data, whatever the provider recorded.
            Assert.False(File.Exists(RevertPath(ToolSubjects.StartupManagerTasks.Id)));
        }
        finally
        {
            Delete(name);
            Cleanup();
        }
    }

    [Fact]
    public async Task TogglingAnAlreadyDisabledTask_RecordsASkip()
    {
        var name = $"optimizerDuck_Test_{Guid.NewGuid():N}";

        try
        {
            Assert.True(Register(name).Ok);
            Assert.Equal(
                OptimizationSuccessResult.Success,
                (await RunToggleAsync(Task(name), enable: false)).Status
            );

            var result = await RunToggleAsync(Task(name), enable: false);

            Assert.Equal(OptimizationSuccessResult.NothingToDo, result.Status);
            Assert.Equal(
                ChangeKind.Skip,
                Assert
                    .Single(ChangeRecordStore.TryRead(ToolSubjects.StartupManagerTasks.Id)!.Steps)
                    .Kind
            );
        }
        finally
        {
            Delete(name);
            Cleanup();
        }
    }

    [Fact]
    public async Task TogglingATaskWindowsNoLongerHas_ReportsAFailureNamingIt()
    {
        var name = $"optimizerDuck_Test_{Guid.NewGuid():N}";

        try
        {
            Assert.True(Register(name).Ok);
            // The page's list still holds it, and Windows does not: the stale entry case.
            Delete(name);

            var result = await RunToggleAsync(Task(name), enable: false);

            Assert.Equal(OptimizationSuccessResult.Failed, result.Status);
            // No failed step to name, so the message says what went wrong, and it names the entry.
            Assert.Empty(result.FailedSteps);
            Assert.Contains(name, result.Message, StringComparison.Ordinal);

            // The provider's own statement is the only step recorded, and nothing was persisted.
            Assert.Equal(
                ChangeKind.NotApplicable,
                Assert
                    .Single(ChangeRecordStore.TryRead(ToolSubjects.StartupManagerTasks.Id)!.Steps)
                    .Kind
            );
            Assert.False(File.Exists(RevertPath(ToolSubjects.StartupManagerTasks.Id)));
        }
        finally
        {
            Delete(name);
            Cleanup();
        }
    }

    /// <summary>
    ///     Runs one toggle the way the page's delegate does: the service records into the run's
    ///     context, and a result it reports as failed becomes the error the run carries.
    /// </summary>
    private static Task<OptimizationResult> RunToggleAsync(StartupTask task, bool enable) =>
        TestRunner
            .New(NewRevertManager())
            .RunAsync(
                new OperationRequest(
                    ToolSubjects.StartupManagerTasks,
                    "Startup manager task",
                    NullLogger.Instance,
                    RevertPersistence.Disabled,
                    async (_, context) =>
                    {
                        var result = await new StartupManagerService(
                            NullLogger<StartupManagerService>.Instance
                        ).ToggleStartupTask(context, task, enable);

                        return result.Ok
                            ? context.Changes.ToApplyResult()
                            : ApplyResult.False(
                                result.Error
                                    ?? Loc.Instance["Optimization.Apply.Error.AllStepsFailed"]
                            );
                    }
                ),
                new NoProgress(),
                TestContext.Current.CancellationToken
            );

    private static StartupTask Task(string name) => new() { TaskName = name, TaskPath = "\\" };

    private static OpResult Register(string name) =>
        ScheduledTaskService.RegisterTask(
            NewCall(),
            "\\",
            new ScheduledTaskModel
            {
                Name = name,
                Path = "\\",
                FullPath = "\\" + name,
                ExecutablePath = "cmd.exe",
                Arguments = "/c exit 0",
                IsEnabled = true,
            }
        );

    private static void Delete(string name) =>
        ScheduledTaskService.DeleteTask(NewCall(), "\\" + name);

    private static OpCall NewCall() =>
        new() { Changes = new ChangeSet(), Logger = NullLogger.Instance };

    private static string RevertPath(Guid id) => Path.Combine(Shared.RevertDirectory, id + ".json");

    private static RevertManager NewRevertManager() =>
        new(
            NullLogger<RevertManager>.Instance,
            TestShell.New(),
            new PowerPlanService(NullLogger<PowerPlanService>.Instance),
            TimeProvider.System
        );

    private static void Cleanup()
    {
        var id = ToolSubjects.StartupManagerTasks.Id;

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

    private sealed class NoProgress : IProgress<ProcessingProgress>
    {
        public void Report(ProcessingProgress value) { }
    }
}
