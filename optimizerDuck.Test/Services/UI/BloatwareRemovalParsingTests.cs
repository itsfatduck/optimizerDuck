using optimizerDuck.Services.UI;

namespace optimizerDuck.Test.Services.UI;

public class BloatwareRemovalParsingTests
{
    [Fact]
    public void ParseRemovalOutput_RemovedPackage_ReportsSuccess()
    {
        var result = BloatwareService.ParseRemovalOutput(
            "Removed: Test.Package_1.0.0.0_x64__abc\nRemoved provisioned package: Test.Package\n"
        );

        Assert.True(result.Succeeded);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void ParseRemovalOutput_RefusedPackage_CarriesTheLine()
    {
        var result = BloatwareService.ParseRemovalOutput(
            "Failed: Test.Package_1.0.0.0_x64__abc. Error: the package is in use\n"
        );

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Contains("in use"));
    }

    [Fact]
    public void ParseRemovalOutput_RefusedProvisionedPackage_IsAFailure()
    {
        var result = BloatwareService.ParseRemovalOutput(
            "Failed removing provisioned package: Test.Package. Error: access denied\n"
        );

        Assert.False(result.Succeeded);
        Assert.Single(result.Failures);
    }

    [Fact]
    public void ParseRemovalOutput_NothingInstalled_IsNotAFailure()
    {
        var result = BloatwareService.ParseRemovalOutput("No installed package found for Test\n");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ParseRemovalOutput_SkippedProvisionedRemoval_IsNotAFailure()
    {
        var result = BloatwareService.ParseRemovalOutput(
            "Removed: Test.Package_1.0.0.0_x64__abc\nSkipping provisioned package removal (disabled by user)\n"
        );

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ParseRemovalOutput_EmptyOutput_ReportsNoFailureOnItsOwn()
    {
        // The shell exit code decides that case, not the parser.
        Assert.True(BloatwareService.ParseRemovalOutput(string.Empty).Succeeded);
    }
}
