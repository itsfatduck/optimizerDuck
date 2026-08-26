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

    [Fact]
    public void MapProgress_ValueExceedsTotal_ClampsToTotal()
    {
        var result = ProcessingDialog.MapProgress(false, 15, 10);

        Assert.Equal(TaskBarProgressState.Normal, result.State);
        Assert.Equal(10, result.Current);
        Assert.Equal(10, result.Total);
    }

    [Fact]
    public void MapProgress_NegativeValue_ClampsToZero()
    {
        var result = ProcessingDialog.MapProgress(false, -3, 10);

        Assert.Equal(TaskBarProgressState.Normal, result.State);
        Assert.Equal(0, result.Current);
    }
}
