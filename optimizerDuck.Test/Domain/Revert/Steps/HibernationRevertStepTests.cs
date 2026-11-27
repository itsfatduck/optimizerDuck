using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.Test.TestDoubles;

namespace optimizerDuck.Test.Domain.Revert.Steps;

public class HibernationRevertStepTests
{
    [Fact]
    public void ToData_And_FromData_RoundTripBothKnownStates()
    {
        foreach (var present in new[] { true, false })
        {
            var original = new HibernationRevertStep { WasPresent = present };

            var restored = HibernationRevertStep.FromData(original.ToData());

            Assert.Equal(present, restored.WasPresent);
        }
    }

    [Fact]
    public void FromData_PayloadWrittenBeforeTheChange_KeepsItsBoolean()
    {
        var payload = JsonNode.Parse("""{ "WasPresent": true }""")!.AsObject();

        Assert.True(HibernationRevertStep.FromData(payload).WasPresent);
    }

    [Fact]
    public void FromData_MissingOrNullState_MeansUnknown()
    {
        Assert.Null(HibernationRevertStep.FromData(new JsonObject()).WasPresent);
        Assert.Null(
            HibernationRevertStep
                .FromData(
                    new JsonObject { [nameof(HibernationRevertStep.WasPresent)] = null }
                )
                .WasPresent
        );
    }

    [Fact]
    public async Task ExecuteAsync_UnknownState_ChangesNothingAndSucceeds()
    {
        // An unknown state restores nothing, so a revert cannot create a hibernation file
        // that never existed.
        var before = HibernationService.IsHibernationFilePresent();
        var step = new HibernationRevertStep { WasPresent = null };

        var result = await step.ExecuteAsync(TestShell.Context(), NullLogger.Instance);

        Assert.True(result);
        Assert.True(step.StateUnknown);
        Assert.Equal(before, HibernationService.IsHibernationFilePresent());
    }
}
