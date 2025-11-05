namespace optimizerDuck.Models;

public record struct AppxClassificationRaw(
    List<List<AppxPackage>> SafeApps,
    List<List<AppxPackage>> CautionApps
);