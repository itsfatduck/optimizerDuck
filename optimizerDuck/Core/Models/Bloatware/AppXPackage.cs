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

    public string Name { get; init; }
    public string PackageFullName { get; init; }
    public string Publisher { get; init; }
    public string Version { get; init; }
    public string InstallLocation { get; init; }
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
        AppRisk.Unknown => new RiskVisual
        {
            Display = Translations.Bloatware_UI_Risk_Unknown,
            Icon = SymbolRegular.ShieldQuestion24
        },
        _ => new RiskVisual
        {
            Display = Translations.Optimizer_UI_Risk_Safe,
            Icon = SymbolRegular.ShieldCheckmark24
        }
    };
};