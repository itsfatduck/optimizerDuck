using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using optimizerDuck.Domain.Exceptions;
using optimizerDuck.Domain.Revert.Steps;

namespace optimizerDuck.Test.Domain.Revert.Steps;

public class PowerPlanRevertStepTests
{
    [Fact]
    public void RoundTrip_PreservesSchemeIds()
    {
        var original = new PowerPlanRevertStep
        {
            PreviousSchemeId = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e"),
            InstalledSchemeId = Guid.Parse("8ae61178-2c55-43f2-afb2-f83725823657"),
        };

        var restored = PowerPlanRevertStep.FromData(original.ToData());

        Assert.Equal(original.PreviousSchemeId, restored.PreviousSchemeId);
        Assert.Equal(original.InstalledSchemeId, restored.InstalledSchemeId);
        Assert.Equal("PowerPlan", restored.Type);
    }

    [Fact]
    public void FromData_MissingIds_ThrowsFailClosed()
    {
        Assert.Throws<StepExecutionException>(() => PowerPlanRevertStep.FromData(new JObject()));
        Assert.Throws<StepExecutionException>(() =>
            PowerPlanRevertStep.FromData(JToken.Parse("{}"))
        );
    }
}
