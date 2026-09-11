using optimizerDuck.Domain.Optimizations.Models.Bloatware;
using optimizerDuck.Services.UI;

namespace optimizerDuck.Test.Services.UI;

public class BloatwareServiceTests
{
    [Theory]
    [InlineData("Microsoft.BingNews", "Microsoft.BingNews_4.53.33252.0_x64__8wekyb3d8bbwe", true)]
    [InlineData(
        "Microsoft.WindowsCalculator",
        "Microsoft.WindowsCalculator_11.2210.0.0_x64__8wekyb3d8bbwe",
        true
    )]
    [InlineData("Valid.App-Name_1", "Valid.App-Name_1_1.0.0.0_neutral__8wekyb3d8bbwe", true)]
    [InlineData("Evil$(whoami)", "Evil$(whoami)_1.0.0.0_x64__8wekyb3d8bbwe", false)]
    [InlineData("Evil;rmdir", "Evil;rmdir_1.0.0.0_x64__8wekyb3d8bbwe", false)]
    [InlineData("", "", false)]
    [InlineData("ValidName", "Invalid_Full_Name_Format", false)]
    public void IsValidPackage_ValidatesPackageIdentifiers(
        string name,
        string fullName,
        bool expected
    )
    {
        var package = new AppXPackage
        {
            Name = name,
            PackageFullName = fullName,
            Publisher = "Publisher",
            Version = "1.0.0.0",
            InstallLocation = @"C:\Program Files\WindowsApps\Test",
        };

        var result = BloatwareService.IsValidPackage(package);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParsePackages_SingleObjectJson_ReturnsOnePackage()
    {
        // PowerShell's ConvertTo-Json emits a bare object for one package and an array
        // for several; the single-object shape must not deserialize to an empty list.
        const string json = """
            {"Name":"A.B","PackageFullName":"A.B_1.0.0.0_x64__abc","Publisher":"CN=X","Version":"1.0.0.0","InstallLocation":"","Risk":"Safe"}
            """;

        var packages = BloatwareService.ParsePackages(json);

        var package = Assert.Single(packages);
        Assert.Equal("A.B", package.Name);
        Assert.Equal(AppRisk.Safe, package.Risk);
    }

    [Fact]
    public void ParsePackages_ArrayJson_ReturnsAllPackages()
    {
        const string json = """
            [{"Name":"A.B","PackageFullName":"A.B_1.0.0.0_x64__abc","Publisher":"CN=X","Version":"1.0.0.0","InstallLocation":"","Risk":"Safe"},
             {"Name":"C.D","PackageFullName":"C.D_1.0.0.0_x64__abc","Publisher":"CN=X","Version":"1.0.0.0","InstallLocation":"","Risk":"Caution"}]
            """;

        var packages = BloatwareService.ParsePackages(json);

        Assert.Equal(2, packages.Count);
        Assert.Equal(AppRisk.Caution, packages[1].Risk);
    }
}
