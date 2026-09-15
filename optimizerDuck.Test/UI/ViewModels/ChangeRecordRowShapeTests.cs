using optimizerDuck.Domain.Execution;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using optimizerDuck.UI.ViewModels.Dialogs;

namespace optimizerDuck.Test.UI.ViewModels;

public class ChangeRecordRowShapeTests
{
    private static ChangeRecordStep RegistryWrite() =>
        new()
        {
            Name = "Registry",
            Description = "Write registry value: HKLM\\Test\\A",
            Kind = ChangeKind.Change,
            Operation = "registry.write",
            Target = @"HKLM\Test\A",
            ValueName = "AllowTelemetry",
            ValueType = "DWord",
            PreviousValue = "1",
            NewValue = "0",
            HasValuePair = true,
        };

    [Fact]
    public void StepThatFoundNothing_SaysWhyItDidNotApply()
    {
        var step = new ChangeRecordStepViewModel(
            new ChangeRecordStep
            {
                Name = "Service",
                Description = "Service 'Gone' not found (not present)",
                Kind = ChangeKind.NotApplicable,
                Operation = "service.startup",
                Target = "Gone",
                Reason = "service.notFound",
            }
        );

        // "Not applicable" alone says nothing, so the row carries the reason in the UI
        // language.
        Assert.Contains(
            step.Fields,
            field => field.Value == Loc.Instance["Optimizer.Details.Reason.ServiceNotFound"]
        );
    }

    [Fact]
    public void StepWithAReasonTheUiDoesNotKnow_ShowsNoCode()
    {
        var step = new ChangeRecordStepViewModel(
            new ChangeRecordStep
            {
                Name = "Service",
                Kind = ChangeKind.NotApplicable,
                Operation = "service.startup",
                Target = "Gone",
                Reason = "something.new",
            }
        );

        // A code the UI cannot word is left out of the row.
        Assert.DoesNotContain(step.Fields, field => field.Value == "something.new");
    }

    [Fact]
    public void StepThatChangedSomething_SaysWhatItDid()
    {
        var applied = new ChangeRecordStepViewModel(RegistryWrite());
        var reverted = new ChangeRecordStepViewModel(RegistryWrite(), ChangeRecordOperation.Revert);

        Assert.True(applied.HasOperation);
        Assert.True(reverted.HasOperation);

        // The revert puts the value back, so it must not read like the apply.
        Assert.NotEqual(applied.OperationLabel, reverted.OperationLabel);
    }

    [Fact]
    public void StepThatWroteNothing_StatesWhatItFoundInsteadOfAnAction()
    {
        var step = new ChangeRecordStepViewModel(
            new ChangeRecordStep
            {
                Name = "Service",
                Description = "Service 'DiagTrack' is already set to Disabled (skipped)",
                Kind = ChangeKind.Skip,
                Operation = "service.startup",
                Target = "DiagTrack",
                PreviousValue = "Disabled",
            }
        );

        // No action is claimed, and the row shows the state it found.
        Assert.False(step.HasOperation);
        Assert.True(step.HasFields);
        Assert.Contains(step.Fields, field => field.Value == "DiagTrack");
        Assert.Contains(step.Fields, field => field.Value.Length > 0 && field.Label.Length > 0);
    }

    [Fact]
    public void RefusedStep_ShowsWhatItFoundWithoutABeforeAndAfter()
    {
        var step = new ChangeRecordStepViewModel(
            new ChangeRecordStep
            {
                Name = "Service",
                Description = "Access to service 'WaaSMedicSvc' is denied by Windows",
                Kind = ChangeKind.Refused,
                Operation = "service.startup",
                Target = "WaaSMedicSvc",
                PreviousValue = "Manual",
            }
        );

        // The service it named and the state it found, and no before and after it cannot know.
        Assert.False(step.HasOperation);
        Assert.Equal(2, step.Fields.Count);
        Assert.Contains(step.Fields, field => field.Value == "WaaSMedicSvc");
        Assert.Contains(step.Fields, field => field.Value == "Manual");
        Assert.All(step.Fields, field => Assert.False(string.IsNullOrWhiteSpace(field.Label)));
    }

    [Fact]
    public void UsbPass_ShowsHowManyDevicesItTouchedWithoutABeforeAndAfter()
    {
        var step = new ChangeRecordStepViewModel(
            new ChangeRecordStep
            {
                Name = "USB power",
                Description = "Disable USB power saving for 2 device(s)",
                Kind = ChangeKind.Change,
                Operation = "usb.power",
                NewValue = "2",
            }
        );

        var field = Assert.Single(step.Fields);
        Assert.Equal("2", field.Value);
        Assert.False(string.IsNullOrWhiteSpace(field.Label));

        // Facts are the whole story, so the English line written for the log stays out of the row.
        Assert.False(step.ShowDescription);
    }

    [Fact]
    public void StepThatWroteNothingNeverFallsBackToTheLogText()
    {
        var step = new ChangeRecordStepViewModel(
            new ChangeRecordStep
            {
                Name = "Registry",
                Description = @"Write registry value: HKCU\Control Panel\Keyboard\KeyboardSpeed",
                Kind = ChangeKind.Skip,
                Operation = "registry.write",
                Target = @"HKCU\Control Panel\Keyboard",
                ValueName = "KeyboardSpeed",
                ValueType = "String",
                PreviousValue = "2",
                NewValue = "2",
                HasValuePair = true,
            }
        );

        Assert.False(step.HasOperation);
        Assert.True(step.HasFields);

        // The chips state what it found, so the row does not repeat it in English.
        Assert.False(step.ShowDescription);
    }

    [Fact]
    public void StepWithoutFacts_FallsBackToTheLogText()
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
        Assert.True(step.ShowDescription);
    }

    [Fact]
    public void RevertOfAUsbPass_SaysItTurnsPowerSavingBackOn()
    {
        var step = new ChangeRecordStepViewModel(
            new ChangeRecordStep
            {
                Name = "USB power",
                Description = "Disable USB power saving for 2 device(s)",
                Kind = ChangeKind.Change,
                Operation = "usb.power",
                NewValue = "2",
            },
            ChangeRecordOperation.Revert
        );

        Assert.True(step.HasOperation);
        Assert.Equal("2", Assert.Single(step.Fields).Value);
    }
}
