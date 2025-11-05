namespace optimizerDuck.Models;

public record struct AppxPackage(
    string DisplayName,
    string Name,
    string Version,
    string InstallLocation
);