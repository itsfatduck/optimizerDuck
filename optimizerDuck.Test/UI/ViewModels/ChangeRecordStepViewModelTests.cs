using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.Test.TestDoubles;
using optimizerDuck.UI.ViewModels.Dialogs;

namespace optimizerDuck.Test.UI.ViewModels;

public class ChangeRecordStepViewModelTests
{
    private static ChangeRecordStep RegistryWriteStep() =>
        new()
        {
            Name = "Registry",
            Description = "Write registry value: HKLM\\Test\\A",
            Kind = ChangeKind.Change,
            Operation = "registry.write",
            Target = @"HKLM\Test\A",
            ValueName = "AllowTelemetry",
            ValueType = "DWord",
            PreviousValue = null,
            NewValue = "0",
            HasValuePair = true,
        };

    [Fact]
    public void RegistryWrite_ShowsTheFactsAsFields()
    {
        var step = new ChangeRecordStepViewModel(RegistryWriteStep());

        Assert.True(step.HasOperation);
        Assert.True(step.HasFields);
        Assert.Contains(step.Fields, f => f.Value == @"HKLM\Test\A");

        // Windows' own name for the type, so it reads the same in every language.
        Assert.Contains(step.Fields, f => f.Value == "REG_DWORD");

        // The name inside the key is what the step wrote, so it is a chip of its own.
        Assert.Contains(step.Fields, f => f.Value == "AllowTelemetry");
        Assert.Contains(step.Fields, f => f.Value == "0");
    }

    [Fact]
    public void ValueThatWasNotThere_SaysSoInsteadOfShowingNothing()
    {
        var step = new ChangeRecordStepViewModel(RegistryWriteStep());

        // Path, value name, type, previous and new, in that order, each readable.
        Assert.Equal(5, step.Fields.Count);
        Assert.All(
            step.Fields,
            field =>
            {
                Assert.False(string.IsNullOrWhiteSpace(field.Label));
                Assert.False(string.IsNullOrWhiteSpace(field.Value));
            }
        );
        Assert.Equal("0", step.Fields[4].Value);
        Assert.NotEqual("0", step.Fields[3].Value);
    }

    [Fact]
    public void StepWithoutFacts_KeepsTheLogDescription()
    {
        var step = new ChangeRecordStepViewModel(
            new ChangeRecordStep
            {
                Name = "Shell",
                Description = "Run: powershell -File x.ps1",
                Kind = ChangeKind.Change,
            }
        );

        Assert.False(step.HasOperation);
        Assert.False(step.HasFields);
        Assert.Equal("Run: powershell -File x.ps1", step.Description);
    }

    [Fact]
    public void ProviderName_IsShownInTheUiLanguage()
    {
        var step = new ChangeRecordStepViewModel(
            new ChangeRecordStep
            {
                Name = "Scheduled Task",
                Description = "Disable scheduled task: \\x",
                Kind = ChangeKind.Skip,
            }
        );

        Assert.Equal(Loc.Instance["Optimizer.Details.Provider.ScheduledTask"], step.Name);
    }

    [Fact]
    public void ProviderName_UnknownProvider_KeepsTheRecordedName()
    {
        var step = new ChangeRecordStepViewModel(
            new ChangeRecordStep { Name = "SomeFutureProvider", Kind = ChangeKind.Change }
        );

        Assert.Equal("SomeFutureProvider", step.Name);
    }

    [Fact]
    public void RowFromARecordedChange_CarriesItsFactsAndItsIndex()
    {
        // The failure list builds its rows from the steps still in memory, so it has to read the
        // same way the record does.
        var row = new ChangeRecordStepViewModel(
            new Change
            {
                Index = 3,
                Name = "Service",
                Description = "Change service 'DiagTrack' to Disabled startup",
                Kind = ChangeKind.Change,
                Ok = false,
                Error = "Access denied",
                ErrorDetail = "System.UnauthorizedAccessException",
                Detail = new ServiceStartupDetail
                {
                    ServiceName = "DiagTrack",
                    PreviousStartupType = ServiceStartupType.Automatic,
                    NewStartupType = ServiceStartupType.Disabled,
                },
            }
        );

        Assert.Equal(3, row.Index);
        Assert.True(row.HasOperation);
        Assert.True(row.HasFields);
        Assert.False(row.ShowDescription);
        Assert.Equal("Access denied", row.Detail);
        Assert.True(row.HasErrorDetail);
        Assert.Contains(row.Fields, field => field.Value == "DiagTrack");
    }

    [Fact]
    public void HowAStepWentIsShownOnlyWhenTheRecordCarriesIt()
    {
        var plain = new ChangeRecordStepViewModel(
            new ChangeRecordStep
            {
                Name = "Registry",
                Kind = ChangeKind.Change,
                RecordedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Local),
                ElapsedMs = 12,
            }
        );

        Assert.Contains(
            plain.Fields,
            field => field.Label == Loc.Instance["Optimizer.Details.Field.At"]
        );
        Assert.Contains(
            plain.Fields,
            field =>
                field.Label == Loc.Instance["Optimizer.Details.Field.Took"]
                && field.Value == "12 ms"
        );

        // A step that succeeded has no code and no second attempt to report.
        Assert.DoesNotContain(
            plain.Fields,
            field => field.Label == Loc.Instance["Optimizer.Details.Field.ErrorCode"]
        );
        Assert.DoesNotContain(
            plain.Fields,
            field => field.Label == Loc.Instance["Optimizer.Details.Field.Attempt"]
        );

        var retried = new ChangeRecordStepViewModel(
            new ChangeRecordStep
            {
                Name = "Service",
                Kind = ChangeKind.Change,
                Ok = false,
                Error = "Access denied",
                NativeErrorCode = 5,
                Attempt = 2,
                ElapsedMs = 1500,
            }
        );

        Assert.Contains(
            retried.Fields,
            field =>
                field.Label == Loc.Instance["Optimizer.Details.Field.ErrorCode"]
                && field.Value == "5"
        );
        Assert.Contains(
            retried.Fields,
            field =>
                field.Label == Loc.Instance["Optimizer.Details.Field.Attempt"] && field.Value == "2"
        );
        Assert.Contains(
            retried.Fields,
            field =>
                field.Label == Loc.Instance["Optimizer.Details.Field.Took"]
                && field.Value == "1.5 s"
        );
    }

    [Fact]
    public void FailedStep_IsShownAsAFailureWithItsReason()
    {
        var step = new ChangeRecordStepViewModel(
            new ChangeRecordStep
            {
                Name = "Registry",
                Description = "Write registry value: HKLM\\Test\\B",
                Kind = ChangeKind.Change,
                Ok = false,
                Error = "Access denied",
            }
        );

        Assert.True(step.HasDetail);
        Assert.Equal("Access denied", step.Detail);
    }
}

public class ChangeRecordRevertTests
{
    private static ChangeRecord Applied() =>
        new()
        {
            Id = Guid.NewGuid(),
            OptimizationKey = "RevertTest",
            LogName = "Revert test",
            AppliedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Local),
            Outcome = "Success",
            Steps =
            [
                new ChangeRecordStep
                {
                    Name = "Registry",
                    Description = "Write registry value",
                    Kind = ChangeKind.Change,
                    Operation = "registry.write",
                    Target = @"HKLM\Test\A",
                    ValueType = "DWord",
                    PreviousValue = "1",
                    NewValue = "0",
                    HasValuePair = true,
                },
                new ChangeRecordStep
                {
                    Name = "Service",
                    Description = "Service already configured",
                    Kind = ChangeKind.Skip,
                    Operation = "service.startup",
                    Target = "DiagTrack",
                },
            ],
        };

    [Fact]
    public void Reverted_DescribesTheUndoneStepsWithTheirValuesSwapped()
    {
        // The apply wrote 0 where 1 was, so the revert writes 1 where 0 is.
        var reverted = Applied().Reverted(new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Local));

        Assert.Equal(ChangeRecordOperation.Revert, reverted.Operation);
        Assert.NotNull(reverted.RevertedAt);

        // Only the step that changed something was undone, and it now reads the other way round.
        var step = Assert.Single(reverted.Steps);
        Assert.Equal("0", step.PreviousValue);
        Assert.Equal("1", step.NewValue);
        Assert.Equal(1, reverted.ChangedCount);
    }

    [Fact]
    public void Reverted_KeepsTheOriginalRecord()
    {
        var applied = Applied();

        _ = applied.Reverted(DateTime.Now);

        Assert.Equal(ChangeRecordOperation.Apply, applied.Operation);
        Assert.Equal("1", applied.Steps[0].PreviousValue);
        Assert.Equal(2, applied.Steps.Count);
    }

    [Fact]
    public void ApplyAgain_StartsFromAFreshApplyRecord()
    {
        var record = ChangeRecord.From(new ChangeSet(), new StubItem(), "Success");

        Assert.Equal(ChangeRecordOperation.Apply, record.Operation);
        Assert.Null(record.RevertedAt);
    }

    [Fact]
    public void RowFromARecordedChange_CarriesItsReason()
    {
        var row = new ChangeRecordStepViewModel(
            new Change
            {
                Index = 1,
                Name = "Service",
                Description = "Service 'Gone' not found (not present)",
                Kind = ChangeKind.NotApplicable,
                Detail = new ServiceStartupDetail
                {
                    ServiceName = "Gone",
                    Reason = "service.notFound",
                },
            }
        );

        Assert.Contains(
            row.Fields,
            field => field.Value == Loc.Instance["Optimizer.Details.Reason.ServiceNotFound"]
        );
    }

    [Fact]
    public void FailureListAfterARevert_WordsItsRowsAsARevert()
    {
        var change = new Change
        {
            Index = 1,
            Name = "Service",
            Description = "Change service 'X' to Disabled startup",
            Kind = ChangeKind.Change,
            Ok = false,
            Error = "Access denied",
            Detail = new ServiceStartupDetail
            {
                ServiceName = "X",
                PreviousStartupType = ServiceStartupType.Automatic,
                NewStartupType = ServiceStartupType.Disabled,
            },
        };

        var applied = new ChangeRecordStepViewModel(change);
        var reverted = new ChangeRecordStepViewModel(change, ChangeRecordOperation.Revert);

        // The same failed step reads differently depending on which run it belongs to.
        Assert.True(applied.HasOperation);
        Assert.NotEqual(applied.OperationLabel, reverted.OperationLabel);

        var list = new OptimizationResultDialogViewModel([change], ChangeRecordOperation.Revert);
        Assert.Equal(ChangeRecordOperation.Revert, Assert.Single(list.FailedSteps).Operation);
    }

    private sealed class StubItem : StubOptimization
    {
        public override string OptimizationKey => "Stub";

        public override string Name => "Stub";

        public override string ShortDescription => "Stub";

        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            return Task.FromResult(ApplyResult.True());
        }
    }
}
