namespace optimizerDuck.Core.Models.Bloatware;

public record AppxPackage
{
    public bool IsSelected { get; set; } = false;
    
    public string Name { get; init; }
    public string PackageFullName { get; init; }
    public string Publisher { get; init; }
    public string Version { get; init; }
    public string InstallLocation { get; init; }
    public DateTime InstallDate { get; init; }
    public bool NonRemovable { get; init; }
};