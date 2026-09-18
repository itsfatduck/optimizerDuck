using optimizerDuck.Common.Helpers;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Test.Common.Helpers;

/// <summary>
///     Pins that an unhandled failure is reported once, names the failure, and never throws.
/// </summary>
public class UserErrorSurfaceTests
{
    [Fact]
    public void TryReport_TheSameFailureTwice_ReportsItOnce()
    {
        var reports = new List<(string Title, string Message)>();
        var surface = new UserErrorSurface((title, message) => reports.Add((title, message)));
        var failure = new InvalidOperationException("the last action failed");

        Assert.True(surface.TryReport(failure));
        Assert.False(surface.TryReport(failure));

        Assert.Single(reports);
    }

    [Fact]
    public void TryReport_CarriesTheFailureMessageAndTheLogHint()
    {
        var reports = new List<(string Title, string Message)>();
        var surface = new UserErrorSurface((title, message) => reports.Add((title, message)));

        Assert.True(surface.TryReport(new InvalidOperationException("the last action failed")));

        var (title, message) = Assert.Single(reports);
        Assert.Equal(Loc.Instance["Error.Unhandled.Title"], title);
        Assert.Contains("the last action failed", message);
        Assert.Contains(Loc.Instance["Error.Unhandled.LogHint"], message);
    }

    [Fact]
    public void TryReport_DifferentFailures_AreEachReported()
    {
        var reports = new List<(string Title, string Message)>();
        var surface = new UserErrorSurface((title, message) => reports.Add((title, message)));

        Assert.True(surface.TryReport(new InvalidOperationException("first")));
        Assert.True(surface.TryReport(new InvalidOperationException("second")));

        Assert.Equal(2, reports.Count);
    }

    [Fact]
    public void TryReport_WhenThePresenterThrows_DoesNotThrow()
    {
        var surface = new UserErrorSurface(
            (_, _) => throw new InvalidOperationException("no shell")
        );

        Assert.True(surface.TryReport(new InvalidOperationException("the last action failed")));
    }
}
