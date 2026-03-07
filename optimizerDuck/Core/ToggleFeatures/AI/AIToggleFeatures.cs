using optimizerDuck.Core.Models.UI;
using optimizerDuck.Core.ToggleFeatures;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.ToggleFeatures.AI;

public class DisableWindowsCopilot : BaseToggleFeature
{
    public override string Name => "ToggleFeature.DisableWindowsCopilot.Name";
    public override string Description => "ToggleFeature.DisableWindowsCopilot.Description";
    public override OptimizationRisk Risk => OptimizationRisk.Safe;
    public override SymbolRegular Icon => SymbolRegular.Brain24;

    protected new RegistryToggle Toggle { get; } = new()
    {
        Path = @"HKCU\Software\Policies\Microsoft\Windows\Windows Copilot",
        Name = "TurnOffWindowsCopilot",
        OnValue = 1,
        OffValue = 0,
        DefaultValue = 0
    };
}

public class DisableBingInWindowsSearch : BaseToggleFeature
{
    public override string Name => "ToggleFeature.DisableBingInWindowsSearch.Name";
    public override string Description => "ToggleFeature.DisableBingInWindowsSearch.Description";
    public override OptimizationRisk Risk => OptimizationRisk.Safe;
    public override SymbolRegular Icon => SymbolRegular.Search24;

    protected new RegistryToggle Toggle { get; } = new()
    {
        Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Search",
        Name = "BingSearchEnabled",
        OnValue = 0,
        OffValue = 1,
        DefaultValue = 1
    };
}

public class DisableSuggestionsInStart : BaseToggleFeature
{
    public override string Name => "ToggleFeature.DisableSuggestionsInStart.Name";
    public override string Description => "ToggleFeature.DisableSuggestionsInStart.Description";
    public override OptimizationRisk Risk => OptimizationRisk.Safe;
    public override SymbolRegular Icon => SymbolRegular.Lightbulb24;

    protected new RegistryToggle Toggle { get; } = new()
    {
        Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
        Name = "SubscribedContent-338389Enabled",
        OnValue = 0,
        OffValue = 1,
        DefaultValue = 1
    };
}

public class DisableTailoredExperiences : BaseToggleFeature
{
    public override string Name => "ToggleFeature.DisableTailoredExperiences.Name";
    public override string Description => "ToggleFeature.DisableTailoredExperiences.Description";
    public override OptimizationRisk Risk => OptimizationRisk.Safe;
    public override SymbolRegular Icon => SymbolRegular.Person24;

    protected new RegistryToggle Toggle { get; } = new()
    {
        Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Privacy",
        Name = "TailoredExperiencesWithDiagnosticDataEnabled",
        OnValue = 0,
        OffValue = 1,
        DefaultValue = 1
    };
}
