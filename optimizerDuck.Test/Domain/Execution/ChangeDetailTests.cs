using Newtonsoft.Json;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Test.TestDoubles;

namespace optimizerDuck.Test.Domain.Execution;

/// <summary>
///     The typed facts of a step: how long they live, what the kinds that move between
///     two values say they do, and how they survive the record file, including one
///     written by an earlier build.
/// </summary>
public class ChangeDetailTests
{
    [Fact]
    public void OnlyTheKindsThatMoveBetweenTwoValuesSaySo()
    {
        var paired = EveryOperationDetail
            .Items.Where(detail => detail.HasValuePair)
            .Select(detail => detail.GetType().Name)
            .Order()
            .ToList();

        Assert.Equal(
            [
                "HibernationDetail",
                "PowerPlanActivateDetail",
                "PowerSettingDetail",
                "RegistryValueRemoveDetail",
                "RegistryValueWriteDetail",
                "ScheduledTaskDisableDetail",
                "ScheduledTaskEnableDetail",
                "ServiceStartupDetail",
            ],
            paired
        );
    }

    [Fact]
    public void EveryKindDeclaresAnOperationCodeAndTheFileKeepsTheOnesItAlreadyHad()
    {
        // The codes are the wire format, so decoding has to keep matching the names
        // old records carry.
        var codes = EveryOperationDetail.Items.Select(detail => detail.Operation).Order().ToList();

        Assert.Equal(
            [
                "hibernation",
                "power.plan",
                "power.planInstall",
                "power.setting",
                "registry.createKey",
                "registry.delete",
                "registry.deleteKey",
                "registry.write",
                "service.startup",
                "task.disable",
                "task.enable",
                "usb.power",
            ],
            codes
        );
    }

    [Theory]
    [MemberData(nameof(EveryOperationDetail.All), MemberType = typeof(EveryOperationDetail))]
    public void WritingARecordAndReadingItBackKeepsEveryFact(ChangeDetail detail)
    {
        var step = StoredRoundTrip(detail);

        Assert.Equal(detail.Operation, step.Operation);
        Assert.Equal(detail, ChangeDetailCodec.Decode(step));
    }

    [Fact]
    public void ARecordFromAnEarlierBuildReadsTheSameAsBefore()
    {
        // Written by a build that had only the flat fields: no timings, no index, no display name.
        const string json = """
            {
              "Id": "5a4c1f9e-2c3a-4f5b-9c7d-8e1f2a3b4c5d",
              "OptimizationKey": "LegacyTest",
              "LogName": "Legacy test",
              "AppliedAt": "2026-01-02T03:04:05",
              "Operation": "Apply",
              "Outcome": "Success",
              "Steps": [
                {
                  "Name": "Registry",
                  "Description": "Write registry value",
                  "Kind": "Change",
                  "Ok": true,
                  "Operation": "registry.write",
                  "Target": "HKLM\\Test\\A",
                  "ValueName": "V",
                  "ValueType": "DWord",
                  "PreviousValue": null,
                  "NewValue": "0",
                  "HasValuePair": true
                },
                {
                  "Name": "Hibernation",
                  "Description": "Disabled hibernation",
                  "Kind": "Change",
                  "Ok": true,
                  "Operation": "hibernation",
                  "PreviousValue": "Unknown",
                  "NewValue": "Disabled",
                  "HasValuePair": true
                },
                {
                  "Name": "Service",
                  "Description": "Change service startup type",
                  "Kind": "Change",
                  "Ok": true,
                  "Operation": "service.startup",
                  "Target": "DiagTrack",
                  "ValueName": "Left over from the old bag",
                  "PreviousValue": "Automatic",
                  "NewValue": "Disabled",
                  "HasValuePair": true
                }
              ]
            }
            """;

        var record = JsonConvert.DeserializeObject<ChangeRecord>(json);

        Assert.NotNull(record);
        Assert.Equal(3, record.Steps.Count);

        // A value that was not there still reads as absent, which is what the old row said.
        var written = Assert.IsType<RegistryValueWriteDetail>(
            ChangeDetailCodec.Decode(record.Steps[0])
        );
        Assert.Equal(@"HKLM\Test\A", written.Target);
        Assert.Equal("V", written.ValueName);
        Assert.Null(written.PreviousValue);
        Assert.Equal("0", written.NewValue);

        // A state the old build could not read is unknown, not a state it was in.
        var hibernation = Assert.IsType<HibernationDetail>(
            ChangeDetailCodec.Decode(record.Steps[1])
        );
        Assert.Null(hibernation.PreviousPresent);
        Assert.False(hibernation.NewPresent);

        // A field the operation has no meaning for is dropped from the row.
        var service = Assert.IsType<ServiceStartupDetail>(
            ChangeDetailCodec.Decode(record.Steps[2])
        );
        Assert.Equal("DiagTrack", service.ServiceName);
        var row = ChangeDetailPresentation.For(
            service,
            record.Steps[2].Kind,
            ChangeRecordOperation.Apply
        );
        Assert.DoesNotContain(row.Chips, chip => chip.Value == "Left over from the old bag");
    }

    [Fact]
    public void AnOperationThisBuildDoesNotKnowDecodesToNothingSoTheRowKeepsItsDescription()
    {
        var step = new ChangeRecordStep
        {
            Name = "SomeFutureProvider",
            Description = "Did something this build cannot word",
            Kind = ChangeKind.Change,
            Operation = "future.operation",
            Target = "Whatever",
        };

        Assert.Null(ChangeDetailCodec.Decode(step));
        Assert.Empty(
            ChangeDetailPresentation
                .For(ChangeDetailCodec.Decode(step), step.Kind, ChangeRecordOperation.Apply)
                .Chips
        );
    }

    [Fact]
    public void EachStepCarriesWhenItRanAndHowLongItTook()
    {
        var changes = new ChangeSet();

        changes.Add("First", "does nothing", true);
        Thread.Sleep(30);
        changes.Add("Second", "waits", true);

        var first = changes.Changes[0];
        var second = changes.Changes[1];

        Assert.NotNull(first.RecordedAt);
        Assert.NotNull(second.RecordedAt);
        Assert.True(
            second.ElapsedMs >= 25,
            $"expected the wait to be counted, got {second.ElapsedMs}"
        );
        Assert.True(changes.ElapsedMs >= second.ElapsedMs);
        Assert.True(changes.StartedAt <= first.RecordedAt);
    }

    [Fact]
    public void AStepRecordsTheCodeItFailedWithAndCountsItsFirstAttempt()
    {
        var changes = new ChangeSet();

        var written = changes.Add("Registry", "wrote a value", true);
        var refused = changes.Add("Service", "was refused", false, nativeErrorCode: 5);

        // Null is no code, which is not the same as zero.
        Assert.Null(written.NativeErrorCode);
        Assert.Equal(5, refused.NativeErrorCode);
        Assert.Equal(1, written.Attempt);
        Assert.Equal(1, refused.Attempt);
    }

    [Fact]
    public void ARunRecordsTheBuildAndTheWindowsVersionItRanOn()
    {
        var changes = new ChangeSet();
        changes.Add("Registry", "wrote a value", true);

        var record = ChangeRecord.From(changes, new StubItem(), "Success");

        Assert.Equal(Shared.FileVersion, record.AppVersion);
        Assert.False(string.IsNullOrWhiteSpace(record.WindowsVersion));
        Assert.NotNull(record.StartedAt);
        Assert.NotNull(record.ElapsedMs);
    }

    /// One step stored and read back the way the record file does it.
    private static ChangeRecordStep StoredRoundTrip(ChangeDetail detail)
    {
        var changes = new ChangeSet();
        changes.Add("Provider", "Description", true, detail: detail);

        var json = JsonConvert.SerializeObject(
            ChangeRecord.From(changes, new StubItem(), "Success"),
            Formatting.Indented
        );
        var read = JsonConvert.DeserializeObject<ChangeRecord>(json);

        Assert.NotNull(read);
        return Assert.Single(read.Steps);
    }

    private sealed class StubItem : StubOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        ) => Task.FromResult(ApplyResult.True());
    }
}
