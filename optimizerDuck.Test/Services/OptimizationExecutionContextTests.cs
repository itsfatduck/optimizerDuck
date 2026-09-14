using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Test.Services;

public class ChangeSetTests
{
    [Fact]
    public void Add_WithSuccessfulChange_RecordsChange()
    {
        var changes = new ChangeSet();

        changes.Add("TestStep", "Test description", true);

        Assert.Single(changes.Changes);
        Assert.True(changes.HasSuccessfulSteps);
        Assert.Equal("TestStep", changes.Changes[0].Name);
        Assert.Equal(1, changes.Changes[0].Index);
    }

    [Fact]
    public void Add_WithFailedChange_RecordsError()
    {
        var changes = new ChangeSet();

        changes.Add("TestStep", "Test description", false, error: "Test error");

        Assert.Single(changes.Changes);
        Assert.False(changes.Changes[0].Ok);
        Assert.False(changes.HasSuccessfulSteps);
        Assert.Equal("Test error", changes.Changes[0].Error);
    }

    [Fact]
    public void Add_WithRevertStep_StoresRevertData()
    {
        var changes = new ChangeSet();
        var revertStep = new MockRevertStep();

        changes.Add("TestStep", "Test description", true, revertStep);

        Assert.Single(changes.SuccessfulSteps);
        Assert.NotNull(changes.SuccessfulSteps[0].Revert);
    }

    [Fact]
    public void DidApplyAnything_CountsAFailedStepThatCarriesCompensation()
    {
        var changes = new ChangeSet();

        changes.Add(
            "USB power",
            "Two devices changed, a third refused",
            false,
            new MockRevertStep(),
            "a device refused the write"
        );

        // The step changed the machine, so what it recorded is what a revert runs on.
        Assert.True(changes.DidApplyAnything);
        Assert.False(changes.HasSuccessfulSteps);
    }

    [Fact]
    public void DidApplyAnything_IgnoresAFailedStepWithoutCompensation()
    {
        var changes = new ChangeSet();

        changes.Add("Service", "Change startup type", false, error: "access denied");

        Assert.False(changes.DidApplyAnything);
    }

    [Fact]
    public void FailedSteps_ReturnsOnlyFailedChanges()
    {
        var changes = new ChangeSet();

        changes.Add("SuccessStep1", "Description", true);
        changes.Add("FailedStep", "Description", false, error: "Error");
        changes.Add("SuccessStep2", "Description", true);

        var failedSteps = changes.FailedSteps;

        Assert.Single(failedSteps);
        Assert.Equal("FailedStep", failedSteps[0].Name);
    }

    [Fact]
    public void Add_IncrementsSequenceSequentially()
    {
        var changes = new ChangeSet();

        changes.Add("Step1", "Description", true);
        changes.Add("Step2", "Description", true);
        changes.Add("Step3", "Description", true);

        Assert.Equal(1, changes.Changes[0].Index);
        Assert.Equal(2, changes.Changes[1].Index);
        Assert.Equal(3, changes.Changes[2].Index);
    }

    [Fact]
    public void ToApplyResult_Empty_ReturnsSuccess()
    {
        // Deliberate reversal: an item can legitimately match nothing on this machine, so a run
        // that recorded no step at all is nothing to do rather than a failure. A provider that
        // changed something without recording it is caught by the compensation guard instead.
        var changes = new ChangeSet();

        var result = changes.ToApplyResult();

        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ToApplyResult_AllFailed_ReturnsFirstError()
    {
        var changes = new ChangeSet();

        changes.Add("Step1", "Desc1", false, error: "err");
        changes.Add("Step2", "Desc2", false, error: "err2");

        var result = changes.ToApplyResult();

        Assert.Equal("err", result.ErrorMessage);
    }

    [Fact]
    public void ToApplyResult_PartialSuccess_ReturnsSuccess()
    {
        var changes = new ChangeSet();

        changes.Add("Step1", "Desc1", true);
        changes.Add("Step2", "Desc2", false, error: "err");

        var result = changes.ToApplyResult();

        Assert.Null(result.ErrorMessage);

        // Successes and failures stay separable.
        Assert.Single(changes.SuccessfulSteps);
        Assert.Single(changes.FailedSteps);
        Assert.True(changes.SuccessfulSteps[0].Ok);
        Assert.False(changes.FailedSteps[0].Ok);
        Assert.Equal("err", changes.FailedSteps[0].Error);
    }

    [Fact]
    public void Add_Concurrent_IsThreadSafe()
    {
        var changes = new ChangeSet();

        Parallel.For(0, 100, i => changes.Add($"Step{i}", "Description", true));

        Assert.Equal(100, changes.Changes.Count);
        Assert.Equal(100, changes.Changes.Select(c => c.Index).Distinct().Count());
    }
    [Fact]
    public void AddSkip_RecordsSuccessfulSkipWithoutCompensation()
    {
        var changes = new ChangeSet();

        changes.AddSkip("Service", "Service 'x' is already set to Manual (skipped)");

        var change = Assert.Single(changes.Changes);
        Assert.True(change.Ok);
        Assert.Equal(ChangeKind.Skip, change.Kind);
        Assert.Null(change.Revert);
    }

    [Fact]
    public void AddIrreversible_RecordsOneWayStep()
    {
        var changes = new ChangeSet();

        changes.AddIrreversible("Scheduled Task", "Deleted task '\\x'");

        var change = Assert.Single(changes.Changes);
        Assert.True(change.Ok);
        Assert.Equal(ChangeKind.Irreversible, change.Kind);
        Assert.Null(change.Revert);
    }

    [Fact]
    public void DidApplyAnything_IgnoresSkipsAndIrreversibleSteps()
    {
        var changes = new ChangeSet();
        changes.AddSkip("Service", "skipped");
        changes.AddIrreversible("Scheduled Task", "deleted");

        Assert.False(changes.DidApplyAnything);

        changes.Add("Registry", "wrote a value", true, new MockRevertStep());

        Assert.True(changes.DidApplyAnything);
    }

    [Fact]
    public void DidApplyAnything_FailedChangeDoesNotCount()
    {
        var changes = new ChangeSet();

        changes.Add("Registry", "write failed", false, null, "denied");

        Assert.False(changes.DidApplyAnything);
    }
}

public class MockRevertStep : IRevertStep
{
    public string Type => "Mock";
    public string Description => "Mock Description";

    public Task<bool> ExecuteAsync(RevertContext _, ILogger logger)
    {
        return Task.FromResult(true);
    }

    public JObject ToData()
    {
        return new JObject();
    }

    public static MockRevertStep FromData(JObject data)
    {
        return new MockRevertStep();
    }
    [Fact]
    public void AddNotApplicable_RecordsStepWithoutCompensation()
    {
        var changes = new ChangeSet();

        changes.AddNotApplicable("Service", "Service 'x' not found (not present)");

        var change = Assert.Single(changes.Changes);
        Assert.True(change.Ok);
        Assert.Equal(ChangeKind.NotApplicable, change.Kind);
        Assert.Null(change.Revert);
    }

    [Fact]
    public void AddRefused_RecordsStepWithoutCompensation()
    {
        var changes = new ChangeSet();

        changes.AddRefused("Service", "Access to service 'x' is denied by Windows");

        var change = Assert.Single(changes.Changes);
        Assert.True(change.Ok);
        Assert.Equal(ChangeKind.Refused, change.Kind);
        Assert.Null(change.Revert);
    }

    [Fact]
    public void DidApplyAnything_IgnoresNotApplicableAndRefusedSteps()
    {
        var changes = new ChangeSet();
        changes.AddNotApplicable("Service", "not present");
        changes.AddRefused("Service", "protected");

        Assert.False(changes.DidApplyAnything);
    }
}
