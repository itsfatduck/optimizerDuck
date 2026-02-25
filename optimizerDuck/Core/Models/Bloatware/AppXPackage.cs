using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Resources.Languages;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.Models.Bloatware;

public enum AppRisk
{
    Safe,
    Caution,
    Unknown
}

public partial class AppXPackage : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    public string? LogoImage { get; set; }
    public required string Name { get; init; }
    public required string PackageFullName { get; init; }
    public required string Publisher { get; init; }
    public required string Version { get; init; }
    public required string InstallLocation { get; init; }
    public AppRisk Risk { get; init; }

    public RiskVisual RiskVisual => Risk switch
    {
        AppRisk.Safe => new RiskVisual
        {
            Display = Translations.Optimizer_UI_Risk_Safe,
            Icon = SymbolRegular.ShieldCheckmark24
        },
        AppRisk.Caution => new RiskVisual
        {
            Display = Translations.Optimizer_UI_Risk_Moderate,
            Icon = SymbolRegular.Warning24
        },
        _ => new RiskVisual
        {
            Display = Translations.Optimizer_UI_Risk_Safe,
            Icon = SymbolRegular.ShieldCheckmark24
        }
    };

    public bool ShouldVisibleRisk => Risk != AppRisk.Unknown;
};