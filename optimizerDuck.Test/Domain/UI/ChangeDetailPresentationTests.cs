using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Test.TestDoubles;

namespace optimizerDuck.Test.Domain.UI;

/// <summary>
///     How the record words one step: the action it states, the facts it shows, and the fact that
///     every kind of operation has a row of its own.
/// </summary>
public class ChangeDetailPresentationTests
{
    [Fact]
    public void TheFactsCoverEveryKindOfOperationTheDomainHas()
    {
        var kinds = typeof(ChangeDetail)
            .Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(ChangeDetail)) && !type.IsAbstract)
            .Select(type => type.Name)
            .Order()
            .ToList();

        var covered = EveryOperationDetail
            .Items.Select(detail => detail.GetType().Name)
            .Order()
            .ToList();

        // A new kind of operation fails this until it is given facts and a row below.
        Assert.Equal(kinds, covered);
    }

    [Theory]
    [MemberData(nameof(EveryOperationDetail.All), MemberType = typeof(EveryOperationDetail))]
    public void EveryOperationStatesWhatItDidAndReadsDifferentlyWhenPutBack(ChangeDetail detail)
    {
        var applied = ChangeDetailPresentation.For(
            detail,
            ChangeKind.Change,
            ChangeRecordOperation.Apply
        );
        var reverted = ChangeDetailPresentation.For(
            detail,
            ChangeKind.Change,
            ChangeRecordOperation.Revert
        );

        Assert.False(string.IsNullOrEmpty(applied.ActionKey));
        Assert.NotEmpty(applied.Chips);
        Assert.False(string.IsNullOrEmpty(reverted.ActionKey));
        Assert.NotEqual(applied.ActionKey, reverted.ActionKey);
    }

    [Theory]
    [MemberData(nameof(EveryOperationDetail.All), MemberType = typeof(EveryOperationDetail))]
    public void EveryKeyARowCanShowResolvesToText(ChangeDetail detail)
    {
        // The module names its resource keys as literals, so a typo would reach the UI as a raw key.
        foreach (
            var operation in new[] { ChangeRecordOperation.Apply, ChangeRecordOperation.Revert }
        )
        foreach (var kind in new[] { ChangeKind.Change, ChangeKind.Skip })
        {
            var row = ChangeDetailPresentation.For(detail, kind, operation);
            var keys = row
                .Chips.SelectMany(chip =>
                    chip.ValueIsKey ? new[] { chip.LabelKey, chip.Value } : [chip.LabelKey]
                )
                .ToList();

            if (row.ActionKey is { } action)
                keys.Add(action);

            foreach (var key in keys)
            {
                var text = Loc.Instance[key];
                Assert.False(string.IsNullOrWhiteSpace(text), $"no text for {key}");
                Assert.NotEqual(key, text);
            }
        }
    }

    [Fact]
    public void AStepThatWroteNothingStatesWhatItFoundAndNoAction()
    {
        var row = ChangeDetailPresentation.For(
            new ServiceStartupDetail
            {
                ServiceName = "DiagTrack",
                PreviousStartupType = ServiceStartupType.Disabled,
            },
            ChangeKind.Skip,
            ChangeRecordOperation.Apply
        );

        Assert.Null(row.ActionKey);
        Assert.Contains(row.Chips, chip => chip.Value == "DiagTrack" && chip.ValueIsKey == false);
        Assert.Contains(
            row.Chips,
            chip =>
                chip.LabelKey == "Optimizer.Details.Field.StartupState"
                && chip.Value == "Optimizer.Details.Value.Startup.Disabled"
        );
    }

    [Fact]
    public void EveryChipSaysWhetherItsValueIsAWordOrAValue()
    {
        var registry = ChangeDetailPresentation.For(
            new RegistryValueWriteDetail
            {
                Target = @"HKLM\Test\A",
                ValueName = "V",
                ValueType = "DWord",
                PreviousValue = "1",
                NewValue = "0",
            },
            ChangeKind.Change,
            ChangeRecordOperation.Apply
        );

        // A path, a name, a type and a value are shown as they are, never translated.
        Assert.Contains(
            registry.Chips,
            chip => chip.LabelKey == "Optimizer.Details.Field.Path" && !chip.ValueIsKey
        );
        Assert.Contains(registry.Chips, chip => chip.Value == "REG_DWORD" && !chip.ValueIsKey);

        // A startup type is one of the app's own words, so it travels as the key of its wording.
        var service = ChangeDetailPresentation.For(
            new ServiceStartupDetail
            {
                ServiceName = "DiagTrack",
                PreviousStartupType = ServiceStartupType.Automatic,
                NewStartupType = ServiceStartupType.Disabled,
            },
            ChangeKind.Change,
            ChangeRecordOperation.Apply
        );

        Assert.Contains(
            service.Chips,
            chip =>
                chip.LabelKey == "Optimizer.Details.Field.Previous"
                && chip.Value == "Optimizer.Details.Value.Startup.Automatic"
                && chip.ValueIsKey
        );
        Assert.Contains(
            service.Chips,
            chip =>
                chip.LabelKey == "Optimizer.Details.Field.New"
                && chip.Value == "Optimizer.Details.Value.Startup.Disabled"
                && chip.ValueIsKey
        );
    }

    [Fact]
    public void APreviousValueThatWasNotThereSaysSoAndAHibernationStateThatCouldNotBeReadSaysUnknown()
    {
        var absent = ChangeDetailPresentation.For(
            new RegistryValueWriteDetail
            {
                Target = @"HKLM\Test\A",
                ValueName = "V",
                NewValue = "0",
            },
            ChangeKind.Change,
            ChangeRecordOperation.Apply
        );

        Assert.Contains(
            absent.Chips,
            chip =>
                chip.LabelKey == "Optimizer.Details.Field.Previous"
                && chip.Value == "Optimizer.Details.Field.Previous.None"
        );

        // An unreadable state is its own answer: it is never shown as off, and never as absent.
        var unknown = ChangeDetailPresentation.For(
            new HibernationDetail { PreviousPresent = null, NewPresent = false },
            ChangeKind.Change,
            ChangeRecordOperation.Apply
        );

        Assert.Contains(
            unknown.Chips,
            chip =>
                chip.LabelKey == "Optimizer.Details.Field.Previous"
                && chip.Value == "Optimizer.Details.Value.Unknown"
        );
        Assert.DoesNotContain(
            unknown.Chips,
            chip => chip.Value == "Optimizer.Details.Field.Previous.None"
        );
    }

    [Fact]
    public void RestoringHibernationIsWordedAsTurningItBackOn()
    {
        var turnedBackOn = ChangeDetailPresentation.For(
            new HibernationDetail { PreviousPresent = false, NewPresent = true },
            ChangeKind.Change,
            ChangeRecordOperation.Revert
        );

        Assert.Equal("Optimizer.Details.Op.HibernationEnable", turnedBackOn.ActionKey);

        var restoredToAbsent = ChangeDetailPresentation.For(
            new HibernationDetail { PreviousPresent = true, NewPresent = false },
            ChangeKind.Change,
            ChangeRecordOperation.Revert
        );

        Assert.Equal("Optimizer.Details.Op.HibernationRestore", restoredToAbsent.ActionKey);
    }

    [Fact]
    public void TheUsbPassShowsHowManyDevicesItTouchedAndNothingElse()
    {
        var row = ChangeDetailPresentation.For(
            new UsbPowerDetail { DeviceCount = 3 },
            ChangeKind.Change,
            ChangeRecordOperation.Apply
        );

        var chip = Assert.Single(row.Chips);
        Assert.Equal("Optimizer.Details.Field.Devices", chip.LabelKey);
        Assert.Equal("3", chip.Value);
    }

    [Fact]
    public void APowerSettingShowsItsNameAndFallsBackToItsIdentifier()
    {
        var named = ChangeDetailPresentation.For(
            new PowerSettingDetail
            {
                SettingId = "1F0B4FA0-8E9E-4D8A-9C4B-1E0A2F5B7C31",
                SettingName = "Processor performance",
                PreviousValue = "AC 100 / DC 50",
                NewValue = "AC 80 / DC 40",
            },
            ChangeKind.Change,
            ChangeRecordOperation.Apply
        );

        Assert.Equal("Processor performance", named.ActionArg);
        Assert.Contains(
            named.Chips,
            chip =>
                chip.LabelKey == "Optimizer.Details.Field.Setting"
                && chip.Value == "Processor performance"
        );

        var unnamed = ChangeDetailPresentation.For(
            new PowerSettingDetail
            {
                SettingId = "1F0B4FA0-8E9E-4D8A-9C4B-1E0A2F5B7C31",
                PreviousValue = "AC 100 / DC 50",
                NewValue = "AC 80 / DC 40",
            },
            ChangeKind.Change,
            ChangeRecordOperation.Apply
        );

        Assert.Equal("1F0B4FA0-8E9E-4D8A-9C4B-1E0A2F5B7C31", unnamed.ActionArg);
    }

    [Fact]
    public void AReasonTheUiCannotWordIsLeftOutRatherThanShownAsACode()
    {
        var known = ChangeDetailPresentation.For(
            new ServiceStartupDetail { ServiceName = "Gone", Reason = "service.notFound" },
            ChangeKind.NotApplicable,
            ChangeRecordOperation.Apply
        );

        Assert.Contains(
            known.Chips,
            chip =>
                chip.LabelKey == "Optimizer.Details.Field.Reason"
                && chip.Value == "Optimizer.Details.Reason.ServiceNotFound"
        );

        var unknown = ChangeDetailPresentation.For(
            new ServiceStartupDetail { ServiceName = "Gone", Reason = "something.new" },
            ChangeKind.NotApplicable,
            ChangeRecordOperation.Apply
        );

        Assert.DoesNotContain(unknown.Chips, chip => chip.Value == "something.new");
    }
}
