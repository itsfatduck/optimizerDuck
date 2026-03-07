using optimizerDuck.Core.Models.UI;
using optimizerDuck.Core.ToggleFeatures;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.ToggleFeatures.Privacy;

public class DisableTelemetry : BaseToggleFeature
{
    public override string Name => "ToggleFeature.DisableTelemetry.Name";
    public override string Description => "ToggleFeature.DisableTelemetry.Description";
    public override OptimizationRisk Risk => OptimizationRisk.Moderate;
    public override SymbolRegular Icon => SymbolRegular.DataUsage24;

    protected new RegistryToggle Toggle { get; } = new()
    {
        Path = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
        Name = "AllowTelemetry",
        OnValue = 0,
        OffValue = 3,
        DefaultValue = 3
    };
}

public class DisableDiagnosticData : BaseToggleFeature
{
    public override string Name => "ToggleFeature.DisableDiagnosticData.Name";
    public override string Description => "ToggleFeature.DisableDiagnosticData.Description";
    public override OptimizationRisk Risk => OptimizationRisk.Safe;
    public override SymbolRegular Icon => SymbolRegular.ArrowExportUp24;

    protected new RegistryToggle Toggle { get; } = new()
    {
        Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Diagnostics\DiagTrack",
        Name = "ShowedToastAtLevel",
        OnValue = 0,
        OffValue = 1,
        DefaultValue = 1
    };
}

public class DisableFeedbackNotifications : BaseToggleFeature
{
    public override string Name => "ToggleFeature.DisableFeedbackNotifications.Name";
    public override string Description => "ToggleFeature.DisableFeedbackNotifications.Description";
    public override OptimizationRisk Risk => OptimizationRisk.Safe;
    public override SymbolRegular Icon => SymbolRegular.ChatBubblesQuestion24;

    protected new RegistryToggle Toggle { get; } = new()
    {
        Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\FeedbackHub\Privacy",
        Name = "FeedbackStorageAllowed",
        OnValue = 0,
        OffValue = 1,
        DefaultValue = 1
    };
}
