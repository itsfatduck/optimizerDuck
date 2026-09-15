using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Services.History;
using optimizerDuck.Services.Optimization;

namespace optimizerDuck.Test.Services.History;

/// <summary>
///     What a retry leaves in the record of the run: the step it recovered, the attempt it needed,
///     and the promise that a record which cannot be written changes nothing for the user.
/// </summary>
public class ChangeRecordRetryTests : IDisposable
{
    private readonly Guid _id = Guid.NewGuid();

    public void Dispose()
    {
        foreach (
            var path in new[]
            {
                ChangeRecordStore.PathFor(_id),
                ChangeRecordStore.PathFor(_id) + ".tmp",
            }
        )
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Ignore: a leftover record affects nothing else.
            }
        }
    }

    [Fact]
    public async Task ARetryThatRecoveredAStepRecordsTheAttemptThatSucceeded()
    {
        var failed = FailedStep();
        Seed(failed);

        var result = await OptimizationService.RetryFailedStepsWithResultsAsync(
            [failed],
            false,
            NullLogger.Instance,
            optimizationId: _id
        );

        Assert.Empty(result.FailedSteps);
        Assert.Equal(2, Assert.Single(result.RecoveredSteps).Attempt);

        var step = Assert.Single(Read()!.Steps);
        Assert.True(step.Ok);
        Assert.Equal(2, step.Attempt);
        Assert.Equal("registry.write", step.Operation);
        Assert.Equal(@"HKLM\Test\A", step.Target);
        Assert.Equal("0", step.NewValue);
    }

    [Fact]
    public async Task ARetryThatFailedAgainRecordsTheAttemptItReached()
    {
        var failed = FailedStep();
        Seed(failed);

        var again = failed with
        {
            Retry = _ => Task.FromResult(OpResult.Fail("Access denied again")),
        };

        var result = await OptimizationService.RetryFailedStepsWithResultsAsync(
            [again],
            false,
            NullLogger.Instance,
            optimizationId: _id
        );

        Assert.Empty(result.RecoveredSteps);
        Assert.Equal(2, Assert.Single(result.FailedSteps).Attempt);

        var step = Assert.Single(Read()!.Steps);
        Assert.False(step.Ok);
        Assert.Equal(2, step.Attempt);
    }

    [Fact]
    public async Task ARecordThatCannotBeWrittenLeavesTheRetryResultAlone()
    {
        var failed = FailedStep();
        Seed(failed);

        using (
            var _ = new FileStream(
                ChangeRecordStore.PathFor(_id),
                FileMode.Open,
                FileAccess.Read,
                FileShare.None
            )
        )
        {
            var result = await OptimizationService.RetryFailedStepsWithResultsAsync(
                [failed],
                false,
                NullLogger.Instance,
                optimizationId: _id
            );

            // A record that cannot be written is not an error the user sees.
            Assert.Empty(result.FailedSteps);
            Assert.Equal(2, Assert.Single(result.RecoveredSteps).Attempt);
        }

        Assert.False(Assert.Single(Read()!.Steps).Ok);
    }

    [Fact]
    public async Task ARetryWithoutAnItemToReportOnWritesNothing()
    {
        var failed = FailedStep();

        var result = await OptimizationService.RetryFailedStepsWithResultsAsync(
            [failed],
            false,
            NullLogger.Instance
        );

        Assert.Empty(result.FailedSteps);
        Assert.Null(Read());
    }

    private static Change FailedStep() =>
        new()
        {
            Index = 1,
            Name = "Registry",
            Description = "Write registry value: HKLM\\Test\\A",
            Kind = ChangeKind.Change,
            Ok = false,
            Error = "Access denied",
            Detail = new RegistryValueWriteDetail
            {
                Target = @"HKLM\Test\A",
                ValueName = "V",
                ValueType = "DWord",
                NewValue = "0",
            },
            Retry = call =>
            {
                call.Changes.Add(
                    "Registry",
                    "Wrote registry value: HKLM\\Test\\A",
                    true,
                    detail: new RegistryValueWriteDetail
                    {
                        Target = @"HKLM\Test\A",
                        ValueName = "V",
                        ValueType = "DWord",
                        NewValue = "0",
                    }
                );
                return Task.FromResult(OpResult.Success());
            },
        };

    private void Seed(Change failedStep) =>
        ChangeRecordStore.TryWrite(
            new ChangeRecord
            {
                Id = _id,
                OptimizationKey = "RetryRecordTest",
                LogName = "Retry record test",
                AppliedAt = DateTime.Now,
                StartedAt = DateTime.Now,
                ElapsedMs = 10,
                AppVersion = "0.0.0-test",
                WindowsVersion = "10.0.0",
                Outcome = "PartialSuccess",
                Steps = [ChangeRecord.StepOf(failedStep)],
            }
        );

    private ChangeRecord? Read() => ChangeRecordStore.TryRead(_id);
}
