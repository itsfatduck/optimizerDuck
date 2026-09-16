using optimizerDuck.UI.Dialogs;
using Wpf.Ui.TaskBar;

namespace optimizerDuck.Test.UI.Dialogs;

public class ProcessingDialogTests
{
    [Fact]
    public void MapProgress_Indeterminate_ReturnsIndeterminateState()
    {
        var result = ProcessingDialog.MapProgress(true, 3, 10);

        Assert.Equal(TaskBarProgressState.Indeterminate, result.State);
        Assert.Equal(0, result.Current);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public void MapProgress_ZeroTotal_ReturnsIndeterminateState()
    {
        var result = ProcessingDialog.MapProgress(false, 0, 0);

        Assert.Equal(TaskBarProgressState.Indeterminate, result.State);
    }

    [Fact]
    public void MapProgress_NegativeTotal_ReturnsIndeterminateState()
    {
        var result = ProcessingDialog.MapProgress(false, 5, -1);

        Assert.Equal(TaskBarProgressState.Indeterminate, result.State);
    }

    [Fact]
    public void MapProgress_DeterminateWithinRange_ReturnsNormalStateWithValues()
    {
        var result = ProcessingDialog.MapProgress(false, 4, 10);

        Assert.Equal(TaskBarProgressState.Normal, result.State);
        Assert.Equal(4, result.Current);
        Assert.Equal(10, result.Total);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(10, 10)]
    [InlineData(15, 10)]
    public void MapProgress_ValueReachingTotal_ClearsTheBar(int value, int total)
    {
        // A run that reached its total is over, so the bar clears instead of staying full.
        var result = ProcessingDialog.MapProgress(false, value, total);

        Assert.Equal(TaskBarProgressState.None, result.State);
        Assert.Equal(0, result.Current);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public void MapProgress_NegativeValue_ClampsToZero()
    {
        var result = ProcessingDialog.MapProgress(false, -3, 10);

        Assert.Equal(TaskBarProgressState.Normal, result.State);
        Assert.Equal(0, result.Current);
    }
}
