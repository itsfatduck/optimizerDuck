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
}
