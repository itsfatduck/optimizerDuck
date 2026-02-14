using CommunityToolkit.Mvvm.ComponentModel;

namespace optimizerDuck.Core.Models.Bloatware;

public enum AppRisk
{
    Safe,
    Caution
}

public partial class AppxPackage : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    public string Name { get; init; }
    public string PackageFullName { get; init; }
    public string Publisher { get; init; }
    public string Version { get; init; }
    public string InstallLocation { get; init; }
    public AppRisk Risk { get; init; }
};