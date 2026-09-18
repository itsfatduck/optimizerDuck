using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.ScheduledTask;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.History;
using optimizerDuck.Services.Optimization;
using optimizerDuck.Services.Revert;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.Test.TestDoubles;
using optimizerDuck.UI.ViewModels;
using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.Test.Services.UI;

/// <summary>
///     Pins what a tool action on the scheduled tasks page does: its steps run under the tool's
///     subject, a failure names the failed step, and a second toggle starts no second run.
/// </summary>
public class ScheduledTasksToolRunTests
{
    [Fact]
    public async Task ToolRun_ThroughTheTaskProvider_RecordsChangeSkipAndIrreversible()
    {
        var name = $"optimizerDuck_Test_{Guid.NewGuid():N}";
        var fullPath = "\\" + name;
        var runner = TestRunner.New(NewRevertManager());
        var subject = ToolSubjects.ScheduledTasks;

        try
        {
            Assert.True(Register(name).Ok);

            var first = await RunAsync(
                runner,
                subject,
                context => ScheduledTaskService.DisableTask(context, fullPath)
            );

            Assert.Equal(OptimizationSuccessResult.Success, first.Status);
            var record = ChangeRecordStore.TryRead(subject.Id);
            Assert.NotNull(record);
            Assert.Equal(subject.Key, record!.OptimizationKey);
            Assert.Equal(ChangeKind.Change, Assert.Single(record.Steps).Kind);

            var second = await RunAsync(
                runner,
                subject,
                context => ScheduledTaskService.DisableTask(context, fullPath)
            );

            Assert.Equal(OptimizationSuccessResult.NothingToDo, second.Status);
            Assert.Equal(
                ChangeKind.Skip,
                Assert.Single(ChangeRecordStore.TryRead(subject.Id)!.Steps).Kind
            );

            var third = await RunAsync(
                runner,
                subject,
                context => ScheduledTaskService.DeleteTask(context, fullPath)
            );

            // Deleting is irreversible, so the run is a success, not nothing to do.
            Assert.Equal(OptimizationSuccessResult.Success, third.Status);
            var deleted = Assert.Single(ChangeRecordStore.TryRead(subject.Id)!.Steps);
            Assert.Equal(ChangeKind.Irreversible, deleted.Kind);
            // The record carries the outcome the run was classified as.
            Assert.Equal(
                nameof(OptimizationSuccessResult.Success),
                ChangeRecordStore.TryRead(subject.Id)!.Outcome
            );

            // The tool action went through the run lifecycle, which keeps no revert data.
            Assert.False(File.Exists(RevertPath(subject.Id)));
        }
        finally
        {
            ScheduledTaskService.DeleteTask(NewCall(), fullPath);
            Cleanup(subject.Id);
        }
    }

    [Fact]
    public async Task ToolRun_FailedStep_IsReportedAsAFailureWithThatStep()
    {
        var subject = new OperationSubject(Guid.NewGuid(), "ScheduledTasksTestTool", "Test tool");
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
                            error: "access denied"
                        );
                        return Task.FromResult(context.Changes.ToApplyResult());
                    }
                ),
                new NoProgress(),
                TestContext.Current.CancellationToken
            );

            Assert.Equal(OptimizationSuccessResult.Failed, result.Status);
            var failed = Assert.Single(result.FailedSteps);
            Assert.Equal("Scheduled Task", failed.Name);
            Assert.Equal("access denied", failed.Error);
        }
        finally
        {
            Cleanup(subject.Id);
        }
    }

    [Fact]
    public async Task RepeatedToggle_ForOneTask_StartsOneRun()
    {
        await RunInStaThreadAsync(async () =>
        {
            var name = $"optimizerDuck_Test_{Guid.NewGuid():N}";
            var fullPath = "\\" + name;
            var logger = new CapturingLogger<OperationRunner>();
            var viewModelLogger = new CapturingLogger<ScheduledTasksViewModel>();
            var snackbar = new FakeSnackbarService();
            var revertManager = NewRevertManager();
            var runner = TestRunner.New(revertManager, logger);
            var dialogs = new ContentDialogService();
            var viewModel = new ScheduledTasksViewModel(
                new ToolRunPresenter(
                    runner,
                    dialogs,
                    snackbar,
                    NullLogger<ToolRunPresenter>.Instance
                ),
                dialogs,
                snackbar,
                viewModelLogger
            );

            try
            {
                Assert.True(Register(name).Ok);

                await viewModel.OnNavigatedToAsync();
                var task = viewModel.Tasks.FirstOrDefault(t => t.FullPath == fullPath);
                Assert.NotNull(task);
                Assert.True(task!.IsEnabled);

                // The second flip lands while the first run is still writing.
                task.IsEnabled = false;
                task.IsEnabled = true;

                var settled = false;
                for (var attempt = 0; attempt < 100 && !settled; attempt++)
                {
                    await Task.Delay(50);
                    settled = snackbar.Shown.Count > 0 && !task.IsEnabled;
                }

                Assert.True(
                    settled,
                    $"started={logger.Started.Count} shown={snackbar.Shown.Count} "
                        + $"switch={task.IsEnabled} vmLog={string.Join(" | ", viewModelLogger.Messages)} "
                        + $"runLog={string.Join(" | ", logger.Messages)}"
                );
                Assert.Single(logger.Started);
                // The switch shows what Windows has, not what the dropped flip asked for.
                Assert.False(task.IsEnabled);
            }
            finally
            {
                ScheduledTaskService.DeleteTask(NewCall(), fullPath);
                Cleanup(ToolSubjects.ScheduledTasks.Id);
            }
        });
    }

    private static Task<OptimizationResult> RunAsync(
        OperationRunner runner,
        OperationSubject subject,
        Func<OptimizationContext, OpResult> work
    )
    {
        return runner.RunAsync(
            new OperationRequest(
                subject,
                "Scheduled task test",
                NullLogger.Instance,
                RevertPersistence.Disabled,
                (_, context) =>
                    Task.Run(() =>
                    {
                        work(context);
                        return context.Changes.ToApplyResult();
                    })
            ),
            new NoProgress(),
            TestContext.Current.CancellationToken
        );
    }

    private static OpResult Register(string name)
    {
        return ScheduledTaskService.RegisterTask(
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
    }

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

    /// <summary>
    ///     Runs the action on a single-threaded apartment with a dispatcher, without which a dialog
    ///     cannot be touched.
    /// </summary>
    private static Task RunInStaThreadAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource();

        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(dispatcher)
            );
            var frame = new DispatcherFrame();

            dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await action();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
                finally
                {
                    frame.Continue = false;
                }
            });

            Dispatcher.PushFrame(frame);
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return tcs.Task;
    }

    private sealed class NoProgress : IProgress<ProcessingProgress>
    {
        public void Report(ProcessingProgress value) { }
    }

    /// <summary>Records what a component logged, and the runs the lifecycle started.</summary>
    /// <typeparam name="T">The category the logger is created for.</typeparam>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public List<string> Started { get; } = [];

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
            var message = formatter(state, exception);
            Messages.Add(
                $"{logLevel}: {message}{exception switch { null => "", _ => $" [{exception.GetType().Name}: {exception.Message}]" }}"
            );
            if (message.StartsWith("Starting run of", StringComparison.Ordinal))
                Started.Add(message);
        }
    }

    private sealed class FakeSnackbarService : ISnackbarService
    {
        public List<(string Title, string Message)> Shown { get; } = [];

        public TimeSpan DefaultTimeOut { get; set; } = TimeSpan.FromSeconds(3);

        public SnackbarPresenter? GetSnackbarPresenter() => null;

        public void SetSnackbarPresenter(SnackbarPresenter presenter) { }

        public void Show(
            string title,
            string message,
            ControlAppearance appearance,
            IconElement? icon,
            TimeSpan timeout
        )
        {
            Shown.Add((title, message));
        }
    }
}
