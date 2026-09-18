using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.History;
using optimizerDuck.Services.Revert;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.Test.TestDoubles;
using optimizerDuck.UI.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.Test.Services.UI;

/// <summary>
///     Pins what a tool run reports: a failure with nothing to retry goes to a snackbar, a retry
///     runs under the run's own subject, and a dialog that cannot be built never stops the run.
/// </summary>
/// <remarks>
///     Wpf.Ui's boxes resolve theme resources this host does not load, so a presenter that needs
///     one is exercised as the degraded path. Retries go through
///     <see cref="ToolRunPresenter.RetryAsync" />.
/// </remarks>
public class ToolRunPresenterTests
{
    private const string FailureTitle = "Error";

    [Fact]
    public async Task RunAsync_ReportsTheRunEvenWhenNoBoxCanBeBuilt()
    {
        await RunInStaThreadAsync(async () =>
        {
            var subject = NewSubject();
            var dialogs = new FakeContentDialogService();
            var snackbar = new FakeSnackbarService();
            var presenter = NewPresenter(dialogs, snackbar);
            var request = NewRequest(
                subject,
                context =>
                {
                    context.Changes.Add("Scheduled Task", "disabled a task", true);
                    return context.Changes.ToApplyResult();
                }
            );

            try
            {
                var result = await presenter.RunAsync(request);

                // The box could not be built here, and the action still ran and was classified.
                Assert.Equal(OptimizationSuccessResult.Success, result.Status);
                Assert.Empty(snackbar.Shown);

                var record = ChangeRecordStore.TryRead(subject.Id);
                Assert.NotNull(record);
                Assert.Equal(1, record!.ChangedCount);
            }
            finally
            {
                Cleanup(subject.Id);
            }
        });
    }

    [Fact]
    public async Task RetryAsync_UsesTheRunsOwnSubjectAndTheRecordTakesTheRetry()
    {
        await RunInStaThreadAsync(async () =>
        {
            var subject = NewSubject();
            var presenter = NewPresenter(new FakeContentDialogService(), new FakeSnackbarService());
            var request = NewRequest(
                subject,
                context =>
                {
                    context.Changes.Add(
                        "Scheduled Task",
                        "disabled a task",
                        false,
                        null,
                        error: "access denied",
                        retry: retryCall =>
                        {
                            retryCall.Changes.Add("Scheduled Task", "disabled a task", true);
                            return Task.FromResult(OpResult.Success());
                        }
                    );
                    return context.Changes.ToApplyResult();
                }
            );

            try
            {
                var result = await presenter.RunAsync(request);
                Assert.Equal(OptimizationSuccessResult.Failed, result.Status);
                Assert.Single(result.FailedSteps);

                var retry = await presenter.RetryAsync(request, result.FailedSteps);

                var recovered = Assert.Single(retry.RecoveredSteps);
                Assert.Empty(retry.FailedSteps);
                Assert.Equal(2, recovered.Attempt);

                // The retry landed in its run's record, and persisted no revert data.
                var record = ChangeRecordStore.TryRead(subject.Id);
                Assert.NotNull(record);
                Assert.Equal(1, record!.ChangedCount);
                Assert.Equal(0, record.FailedCount);
                Assert.False(File.Exists(RevertPath(subject.Id)));
            }
            finally
            {
                Cleanup(subject.Id);
            }
        });
    }

    [Fact]
    public async Task ReportAsync_FailureWithNothingToRetry_ReportsItInASnackbar()
    {
        await RunInStaThreadAsync(async () =>
        {
            var subject = NewSubject();
            var dialogs = new FakeContentDialogService();
            var snackbar = new FakeSnackbarService();
            var presenter = NewPresenter(dialogs, snackbar);
            var request = NewRequest(
                subject,
                _ => ApplyResult.False("the entry is no longer on this machine")
            );

            try
            {
                var result = await presenter.RunAsync(request);
                Assert.Equal(OptimizationSuccessResult.Failed, result.Status);
                Assert.Empty(result.FailedSteps);

                var reported = await presenter.ReportAsync(request, result, FailureTitle);

                Assert.False(reported);
                var shown = Assert.Single(snackbar.Shown);
                Assert.Equal(FailureTitle, shown.Title);
                Assert.Equal(result.Message, shown.Message);
                Assert.Empty(dialogs.ShownTitles);
            }
            finally
            {
                Cleanup(subject.Id);
            }
        });
    }

    [Fact]
    public async Task ReportAsync_Success_ReportsNothing()
    {
        await RunInStaThreadAsync(async () =>
        {
            var subject = NewSubject();
            var dialogs = new FakeContentDialogService();
            var snackbar = new FakeSnackbarService();
            var presenter = NewPresenter(dialogs, snackbar);
            var request = NewRequest(subject, context => context.Changes.ToApplyResult());

            try
            {
                var result = await presenter.RunAsync(request);
                Assert.Equal(OptimizationSuccessResult.NothingToDo, result.Status);

                var reported = await presenter.ReportAsync(request, result, FailureTitle);

                Assert.True(reported);
                Assert.Empty(snackbar.Shown);
                Assert.Empty(dialogs.ShownTitles);
            }
            finally
            {
                Cleanup(subject.Id);
            }
        });
    }

    private static ToolRunPresenter NewPresenter(
        FakeContentDialogService dialogs,
        FakeSnackbarService snackbar
    ) =>
        new(
            TestRunner.New(NewRevertManager()),
            dialogs,
            snackbar,
            NullLogger<ToolRunPresenter>.Instance
        );

    private static OperationRequest NewRequest(
        OperationSubject subject,
        Func<OptimizationContext, ApplyResult> work
    ) =>
        new(
            subject,
            "Startup manager task",
            NullLogger.Instance,
            RevertPersistence.Disabled,
            (_, context) => Task.FromResult(work(context))
        );

    private static OperationSubject NewSubject() =>
        new(Guid.NewGuid(), "PresenterTestTool", "Presenter test tool");

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

    /// <summary>
    ///     Stands in for the dialog host a real shell provides, and records what was asked of it.
    /// </summary>
    private sealed class FakeContentDialogService : IContentDialogService
    {
        public List<string> ShownTitles { get; } = [];

        public Task<ContentDialogResult> ShowAsync(
            ContentDialog dialog,
            CancellationToken cancellationToken
        )
        {
            ShownTitles.Add(dialog.Title?.ToString() ?? string.Empty);
            return Task.FromResult(ContentDialogResult.None);
        }

        public ContentPresenter? GetDialogHost() => null;

        public ContentDialogHost? GetDialogHostEx() => null;

        public void SetDialogHost(ContentDialogHost dialogHost) { }

        public void SetDialogHost(ContentPresenter contentPresenter) { }

        public ContentPresenter? GetContentPresenter() => null;

        public void SetContentPresenter(ContentPresenter contentPresenter) { }
    }
}
