using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.StartupManager;
using optimizerDuck.Services.UI;

namespace optimizerDuck.Test.Services.UI;

public class StartupManagerServiceTests
{
    [Fact]
    public async Task ToggleStartupApp_UnsupportedLocation_ReturnsFailure()
    {
        var service = new StartupManagerService(NullLogger<StartupManagerService>.Instance);
        var app = new StartupApp
        {
            Name = "Test App",
            Location = StartupAppLocation.RegistryHKCURun,
            PathOrKey = "NoBackslashHere",
        };

        var result = await service.ToggleStartupApp(app, true);

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ToggleStartupTask_MissingTask_ReturnsFailure()
    {
        // A toggle for a task that does not exist must report failure, never log success.
        var service = new StartupManagerService(NullLogger<StartupManagerService>.Instance);
        var call = new OpCall { Changes = new ChangeSet(), Logger = NullLogger.Instance };
        var task = new StartupTask
        {
            TaskName = $"optimizerDuck_Test_Missing_{Guid.NewGuid():N}",
            TaskPath = "\\",
        };

        var result = await service.ToggleStartupTask(call, task, true);

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);

        // The provider recorded what it found, and the service decided this list cannot act on it.
        Assert.Equal(ChangeKind.NotApplicable, Assert.Single(call.Changes.Changes).Kind);
    }

    /// <summary>
    ///     A package family name is the first and last segment of a package full name; the scanner
    ///     builds the state key Windows reports from it.
    /// </summary>
    [Theory]
    [InlineData(
        "Microsoft.WindowsTerminal_1.24.11911.0_x64__8wekyb3d8bbwe",
        "Microsoft.WindowsTerminal_8wekyb3d8bbwe"
    )]
    [InlineData(
        "1527c705-839a-4832-9118-54d4Bd6a0c89_10.0.19640.1000_neutral_neutral_cw5n1h2txyewy",
        "1527c705-839a-4832-9118-54d4Bd6a0c89_cw5n1h2txyewy"
    )]
    [InlineData("Name_1.0.0.0_x64__", null)]
    [InlineData("_1.0.0.0_x64__8wekyb3d8bbwe", null)]
    [InlineData("NoUnderscore", null)]
    [InlineData("", null)]
    public void PackageFamilyName_ReadsTheFirstAndLastSegment(string fullName, string? expected)
    {
        Assert.Equal(expected, StartupManagerService.PackageFamilyName(fullName));
    }

    /// <summary>
    ///     A manifest leaves its name as a resource when the package is localized, and this process
    ///     cannot resolve those, so the scanner falls through instead of showing the raw value.
    /// </summary>
    [Theory]
    [InlineData("Windows Terminal", "Windows Terminal")]
    [InlineData("  XBOX  ", "XBOX")]
    [InlineData("ms-resource:AppStoreName", null)]
    [InlineData("MS-RESOURCE:AppStoreName", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ReadDisplayName_KeepsTextAndRejectsAnUnresolvedResource(
        string? value,
        string? expected
    )
    {
        Assert.Equal(expected, StartupManagerService.ReadDisplayName(value));
    }
}
