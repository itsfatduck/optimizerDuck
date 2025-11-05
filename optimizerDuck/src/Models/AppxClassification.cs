namespace optimizerDuck.Models;

public record struct AppxClassification(
    List<AppxPackage> SafeApps,
    List<AppxPackage> CautionApps
);