using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Resources.Languages;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.Models.Optimization.Bloatware;

/// <summary>
///     Specifies the risk level of removing a bloatware app.
/// </summary>
public enum AppRisk
{
    Safe,
    Caution,
    Unknown
}

/// <summary>
///     Represents an AppX package (UWP app) that can be removed.
/// </summary>
public partial class AppXPackage : ObservableObject
{
    /// <summary>
    ///     Indicates whether this package is selected for removal.
    /// </summary>
    [ObservableProperty] private bool _isSelected;

    /// <summary>
    ///     Path to the package's logo image.
    /// </summary>
    public string? LogoImage { get; set; }

    /// <summary>
    ///     Display name of the app.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     The full package name.
    /// </summary>
    public required string PackageFullName { get; init; }

    /// <summary>
    ///     The publisher of the app.
    /// </summary>
    public required string Publisher { get; init; }

    /// <summary>
    ///     The version of the app.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    ///     The installation location of the app.
    /// </summary>
    public required string InstallLocation { get; init; }

    /// <summary>
    ///     The risk level of removing this app.
    /// </summary>
    public AppRisk Risk { get; init; }

    /// <summary>
    ///     Gets the visual representation of the risk level for UI display.
    /// </summary>
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

    /// <summary>
    ///     Indicates whether the risk should be visible in the UI.
    /// </summary>
    public bool ShouldVisibleRisk => Risk != AppRisk.Unknown;
}