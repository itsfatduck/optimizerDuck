using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.UI;

namespace optimizerDuck.Test.Domain.UI;

public class ChangeKindPresentationTests
{
    [Theory]
    [InlineData(ChangeKind.Change, "Optimizer.Details.Step.Changed")]
    [InlineData(ChangeKind.Skip, "Optimizer.Details.Step.AlreadyCorrect")]
    [InlineData(ChangeKind.NotApplicable, "Optimizer.Details.Step.NotApplicable")]
    [InlineData(ChangeKind.Refused, "Optimizer.Details.Step.Refused")]
    [InlineData(ChangeKind.Irreversible, "Optimizer.Details.Step.Irreversible")]
    public void ToDisplay_MapsEveryKindToItsOwnLabel(ChangeKind kind, string labelKey)
    {
        Assert.Equal(labelKey, kind.ToDisplay().LabelKey);
    }

    [Fact]
    public void ToDisplay_GivesEveryKindItsOwnIcon()
    {
        var icons = Enum.GetValues<ChangeKind>().Select(kind => kind.ToDisplay().Icon).ToList();

        Assert.Equal(icons.Count, icons.Distinct().Count());
    }

    [Fact]
    public void ToDisplay_UnknownKind_FallsBackToTheChangeAppearance()
    {
        // A kind written by a newer version stays visible instead of disappearing.
        var display = ((ChangeKind)999).ToDisplay();

        Assert.Equal("Optimizer.Details.Step.Changed", display.LabelKey);
    }
}
