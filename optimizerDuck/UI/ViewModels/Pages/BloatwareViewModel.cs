using System.Collections.ObjectModel;
using optimizerDuck.Core.Models.Bloatware;

namespace optimizerDuck.UI.ViewModels.Pages;

public class BloatwareViewModel
{
    public ObservableCollection<AppxPackage> AppxPackages { get; private set; } = new();

    public BloatwareViewModel()
    {
        // generate sample AppxPackages
        AppxPackages.Add(new AppxPackage
        {
            Name = "Microsoft.WindowsCalculator",
            PackageFullName = "Microsoft.WindowsCalculator_11.2401.0.0_x64__8wekyb3d8bbwe",
            Publisher = "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US",
            Version = "11.2401.0.0",
            InstallLocation =
                @"C:\Program Files\WindowsApps\Microsoft.WindowsCalculator_11.2401.0.0_x64__8wekyb3d8bbwe",
            InstallDate = DateTime.Now.AddDays(-30),
            NonRemovable = false,
        });

        AppxPackages.Add(new AppxPackage
        {
            Name = "Microsoft.WindowsStore",
            PackageFullName = "Microsoft.WindowsStore_22401.1401.3.0_x64__8wekyb3d8bbwe",
            Publisher = "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US",
            Version = "22401.1401.3.0",
            InstallLocation = @"C:\Program Files\WindowsApps\Microsoft.WindowsStore_22401.1401.3.0_x64__8wekyb3d8bbwe",
            InstallDate = DateTime.Now.AddMonths(-2),
            NonRemovable = true, // system protected
        });

        AppxPackages.Add(new AppxPackage
        {
            Name = "Contoso.MediaPlayer",
            PackageFullName = "Contoso.MediaPlayer_2.5.3.0_x64__abcd1234efgh",
            Publisher = "CN=Contoso Software Ltd, O=Contoso Ltd, C=US",
            Version = "2.5.3.0",
            InstallLocation = @"C:\Program Files\WindowsApps\Contoso.MediaPlayer_2.5.3.0_x64__abcd1234efgh",
            InstallDate = DateTime.Now.AddDays(-7),
            NonRemovable = false,
        });

        AppxPackages.Add(new AppxPackage
        {
            Name = "Fabrikam.PhotoEditor",
            PackageFullName = "Fabrikam.PhotoEditor_5.0.1.0_x86__xyz987654321",
            Publisher = "CN=Fabrikam Inc, O=Fabrikam Inc, C=US",
            Version = "5.0.1.0",
            InstallLocation = @"C:\Program Files\WindowsApps\Fabrikam.PhotoEditor_5.0.1.0_x86__xyz987654321",
            InstallDate = DateTime.Now.AddDays(-120),
            NonRemovable = false,
        });

        AppxPackages.Add(new AppxPackage
        {
            Name = "Microsoft.VCLibs.140.00",
            PackageFullName = "Microsoft.VCLibs.140.00_14.0.32530.0_x64__8wekyb3d8bbwe",
            Publisher = "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US",
            Version = "14.0.32530.0",
            InstallLocation = @"C:\Program Files\WindowsApps\Microsoft.VCLibs.140.00_14.0.32530.0_x64__8wekyb3d8bbwe",
            InstallDate = DateTime.Now.AddYears(-1),
            NonRemovable = true, // framework
        });
    }
}