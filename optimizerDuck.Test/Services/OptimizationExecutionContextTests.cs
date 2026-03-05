using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Execution;
using optimizerDuck.Core.Models.Optimization;
using optimizerDuck.Core.Models.UI;
using OptimizationState = optimizerDuck.Core.Models.UI.OptimizationState;

namespace optimizerDuck.Test.Services;

public class ExecutionScopeTests
{
    [Fact]
    public void RecordStep_WithSuccessfulStep_RecordsStep()
    {
        var logger = NullLogger.Instance;

        using var scope = ExecutionScope.Begin(new MockOptimization(), logger);

        ExecutionScope.RecordStep("TestStep", "Test description", true);

        Assert.Single(scope.ExecutedSteps);
        Assert.True(scope.HasSuccessfulSteps);
        Assert.Equal("TestStep", scope.ExecutedSteps[0].Name);
    }

    [Fact]
    public void RecordStep_WithFailedStep_RecordsFailedStep()
    {
        var logger = NullLogger.Instance;

        using var scope = ExecutionScope.Begin(new MockOptimization(), logger);

        ExecutionScope.RecordStep("TestStep", "Test description", false, error: "Test error");

        Assert.Single(scope.ExecutedSteps);
        Assert.False(scope.ExecutedSteps[0].Success);
        Assert.False(scope.HasSuccessfulSteps);
        Assert.Equal("Test error", scope.ExecutedSteps[0].Error);
    }

    [Fact]
    public void RecordStep_WithRevertStep_RecordsRevertData()
    {
        var logger = NullLogger.Instance;
        var revertStep = new MockRevertStep();

        using var scope = ExecutionScope.Begin(new MockOptimization(), logger);

        ExecutionScope.RecordStep("TestStep", "Test description", true, revertStep);

        Assert.Single(scope.SuccessfulSteps);
        Assert.NotNull(scope.SuccessfulSteps[0].RevertStep);
    }

    [Fact]
    public void FailedSteps_ReturnsOnlyFailedSteps()
    {
        var logger = NullLogger.Instance;

        using var scope = ExecutionScope.Begin(new MockOptimization(), logger);

        ExecutionScope.RecordStep("SuccessStep1", "Description", true);
        ExecutionScope.RecordStep("FailedStep", "Description", false, error: "Error");
        ExecutionScope.RecordStep("SuccessStep2", "Description", true);

        var failedSteps = scope.FailedSteps;

        Assert.Single(failedSteps);
        Assert.Equal("FailedStep", failedSteps[0].Name);
    }

    [Fact]
    public void StepIndex_IncrementsSequentially()
    {
        var logger = NullLogger.Instance;

        using var scope = ExecutionScope.Begin(new MockOptimization(), logger);

        ExecutionScope.RecordStep("Step1", "Description", true);
        ExecutionScope.RecordStep("Step2", "Description", true);
        ExecutionScope.RecordStep("Step3", "Description", true);

        Assert.Equal(1, scope.ExecutedSteps[0].Index);
        Assert.Equal(2, scope.ExecutedSteps[1].Index);
        Assert.Equal(3, scope.ExecutedSteps[2].Index);
    }

    [Fact]
    public void Dispose_ClearsCurrentScope()
    {
        var logger = NullLogger.Instance;

        var scope = ExecutionScope.Begin(new MockOptimization(), logger);

        ExecutionScope.RecordStep("Success", "Description", true);
        ExecutionScope.RecordStep("Fail", "Description", false);

        Assert.NotNull(ExecutionScope.Current);

        scope.Dispose();

        Assert.Null(ExecutionScope.Current);
    }

    [Fact]
    public void Begin_WithLogger_CreatesLightweightScope()
    {
        var logger = NullLogger.Instance;

        using var scope = ExecutionScope.Begin(logger);

        ExecutionScope.RecordStep("TestStep", "Description", true);

        Assert.NotNull(ExecutionScope.Current);
        Assert.Single(scope.ExecutedSteps);
        Assert.Equal(Guid.Empty, scope.OptimizationId);
    }

    [Fact]
    public void Begin_ThrowsIfAlreadyActive()
    {
        var logger = NullLogger.Instance;

        using var scope = ExecutionScope.Begin(new MockOptimization(), logger);

        Assert.Throws<InvalidOperationException>(() =>
            ExecutionScope.Begin(new MockOptimization(), logger));
    }

    [Fact]
    public void RecordStep_WithoutActiveScope_ReturnsNull()
    {
        var result = ExecutionScope.RecordStep("TestStep", "Description", true);

        Assert.Null(result);
    }

    [Fact]
    public void Track_UpdatesStats()
    {
        var logger = NullLogger.Instance;

        using var scope = ExecutionScope.Begin(new MockOptimization(), logger);

        ExecutionScope.Track("Registry", true);
        ExecutionScope.Track("Registry", true);
        ExecutionScope.Track("Registry", false);

        var steps = scope.GetStepResults();
        Assert.Empty(steps);
    }

    [Fact]
    public void GetStepResults_ReturnsOperationStepResults()
    {
        var logger = NullLogger.Instance;

        using var scope = ExecutionScope.Begin(new MockOptimization(), logger);

        ExecutionScope.RecordStep("Step1", "Desc1", true);
        ExecutionScope.RecordStep("Step2", "Desc2", false, error: "err");

        var results = scope.GetStepResults();

        Assert.Equal(2, results.Count);
        Assert.True(results[0].Success);
        Assert.False(results[1].Success);
        Assert.Equal("err", results[1].Error);
    }
}

public class MockOptimization : IOptimization
{
    public Guid Id { get; } = Guid.NewGuid();
    public OptimizationRisk Risk => OptimizationRisk.Safe;
    public string OptimizationKey => "MockOptimization";
    public string Name => "Mock Optimization";
    public string ShortDescription => "Mock description";
    public OptimizationState State { get; set; } = new();

    public Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
    {
        return Task.FromResult(ApplyResult.True());
    }
}

public class MockRevertStep : IRevertStep
{
    public string Type => "Mock";
    public string Description => "Mock Description";

    public static MockRevertStep FromData(JObject data)
    {
        return new MockRevertStep();
    }

    public Task<bool> ExecuteAsync()
    {
        return Task.FromResult(true);
    }

    public JObject ToData()
    {
        return new JObject();
    }
}