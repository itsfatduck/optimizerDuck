using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.ScheduledTask;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.Test.TestDoubles;

namespace optimizerDuck.Test.Services.OptimizationServices;

public class ScheduledTaskServiceTests
{
    [Fact]
    public void GetTaskEnabledState_NonExistentRootTask_ReportsNotFound()
    {
        // Under the always-present root folder a missing task must be reported as
        // "not found" rather than "disabled" or "query failed".
        var path = $"\\optimizerDuck_Test_Missing_{Guid.NewGuid():N}";

        var state = ScheduledTaskService.GetTaskEnabledState(path, NullLogger.Instance);

        Assert.Equal(TaskEnabledState.NotFound, state);
    }

    [Fact]
    public async Task DisableAndEnable_RoundTripThroughTheRecordedRevertSteps()
    {
        // The enable and disable pair is the part optimizations use, so the recorded step has to
        // carry the previous state and put it back. This test creates and deletes its own task.
        var name = $"optimizerDuck_Test_{Guid.NewGuid():N}";
        var fullPath = "\\" + name;
        var model = new ScheduledTaskModel
        {
            Name = name,
            Path = "\\",
            FullPath = fullPath,
            ExecutablePath = "cmd.exe",
            Arguments = "/c exit 0",
            IsEnabled = true,
        };

        try
        {
            var registered = ScheduledTaskService.RegisterTask(
                NewCall(),
                "\\",
                model
            );
            Assert.True(registered.Ok, registered.Error);

            // Disable: the recorded step must carry "was enabled" and restore it.
            var disableCall = NewCall();
            var disabled = ScheduledTaskService.DisableTask(disableCall, fullPath);
            Assert.True(disabled.Ok, disabled.Error);
            var disableStep = Assert.IsType<ScheduledTaskRevertStep>(
                Assert.Single(disableCall.Changes.Changes).Revert
            );
            Assert.True(disableStep.OriginalEnabled);
            Assert.Equal(
                TaskEnabledState.Disabled,
                ScheduledTaskService.GetTaskEnabledState(fullPath, NullLogger.Instance)
            );

            Assert.True(
                await disableStep.ExecuteAsync(TestShell.Context(), NullLogger.Instance)
            );
            Assert.Equal(
                TaskEnabledState.Enabled,
                ScheduledTaskService.GetTaskEnabledState(fullPath, NullLogger.Instance)
            );

            // Disable again first, so enabling really changes the state.
            Assert.True(ScheduledTaskService.DisableTask(NewCall(), fullPath).Ok);

            // Already disabled: the provider records the step and writes nothing, so a caller
            // that delegates here never has to decide on its own.
            var alreadyDisabledCall = NewCall();
            Assert.True(ScheduledTaskService.DisableTask(alreadyDisabledCall, fullPath).Ok);
            var alreadyDisabledStep = Assert.Single(alreadyDisabledCall.Changes.Changes);
            Assert.Equal(ChangeKind.Skip, alreadyDisabledStep.Kind);
            Assert.Null(alreadyDisabledStep.Revert);

            // Enable: the recorded step must carry "was disabled" and restore that.
            var enableCall = NewCall();
            var enabled = ScheduledTaskService.EnableTask(enableCall, fullPath);
            Assert.True(enabled.Ok, enabled.Error);
            var enableStep = Assert.IsType<ScheduledTaskRevertStep>(
                Assert.Single(enableCall.Changes.Changes).Revert
            );
            Assert.False(enableStep.OriginalEnabled);

            Assert.True(await enableStep.ExecuteAsync(TestShell.Context(), NullLogger.Instance));
            Assert.Equal(
                TaskEnabledState.Disabled,
                ScheduledTaskService.GetTaskEnabledState(fullPath, NullLogger.Instance)
            );
        }
        finally
        {
            ScheduledTaskService.DeleteTask(NewCall(), fullPath);
        }

        Assert.Equal(
            TaskEnabledState.NotFound,
            ScheduledTaskService.GetTaskEnabledState(fullPath, NullLogger.Instance)
        );
    }

    private static OpCall NewCall() =>
        new() { Changes = new ChangeSet(), Logger = NullLogger.Instance };
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TaskMissing_RecordsNotApplicableRatherThanFailure(bool disable)
    {
        var fullPath = $@"\{Guid.NewGuid():N}OptimizerDuckMissingTask";
        var call = NewCall();

        var result = disable
            ? ScheduledTaskService.DisableTask(call, fullPath)
            : ScheduledTaskService.EnableTask(call, fullPath);

        // Nothing on this machine to configure, and nothing to undo either.
        Assert.True(result.Ok, result.Error);
        var step = Assert.Single(call.Changes.Changes);
        Assert.Equal(ChangeKind.NotApplicable, step.Kind);
        Assert.Null(step.Revert);
    }
}
