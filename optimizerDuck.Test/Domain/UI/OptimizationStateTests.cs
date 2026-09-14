using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.UI;

namespace optimizerDuck.Test.Domain.UI;

public class OptimizationStateTests
{
    [Fact]
    public void IsAlreadyOptimal_DefaultsToFalseAndIsNeverWrittenToDisk()
    {
        var state = new OptimizationState();
        Assert.False(state.IsAlreadyOptimal);

        var before = Directory.Exists(Shared.RevertDirectory)
            ? Directory.GetFiles(Shared.RevertDirectory).Length
            : 0;

        state.IsAlreadyOptimal = true;

        Assert.True(state.IsAlreadyOptimal);
        var after = Directory.Exists(Shared.RevertDirectory)
            ? Directory.GetFiles(Shared.RevertDirectory).Length
            : 0;
        Assert.Equal(before, after);
    }
}
