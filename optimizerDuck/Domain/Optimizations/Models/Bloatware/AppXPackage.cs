using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Configuration;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Optimizations.Models.Bloatware;

/// <summary>
///     Specifies the risk level of removing a bloatware app.
/// </summary>
public enum AppRisk
{
    Safe,
    Caution,
    Unknown,
}

/// <summary>
///     Represents an AppX package (UWP app) that can be removed.
/// </summary>
/// <remarks>
///     Deriving from <see cref="LocalizedObject" /> makes <see cref="RiskVisual" /> re-resolve
///     when the UI language changes at runtime.
/// </remarks>
public partial class AppXPackage : LocalizedObject
{
    [ObservableProperty]
    private bool _isSelected;

    public string? LogoImage { get; set; }

    public required string Name { get; init; }

    public required string PackageFullName { get; init; }

    public required string Publisher { get; init; }

    public required string Version { get; init; }

    public required string InstallLocation { get; init; }

    public AppRisk Risk { get; init; }

    public RiskVisual RiskVisual =>
        Risk switch
        {
            AppRisk.Safe => new RiskVisual
            {
                Display = Loc.Instance["Optimizer.UI.Risk.Safe"],
                Icon = SymbolRegular.ShieldCheckmark24,
            },
            AppRisk.Caution => new RiskVisual
            {
                Display = Loc.Instance["Optimizer.UI.Risk.Moderate"],
                Icon = SymbolRegular.Warning24,
            },
            _ => new RiskVisual
            {
                Display = Loc.Instance["Optimizer.UI.Risk.Safe"],
                Icon = SymbolRegular.ShieldCheckmark24,
            },
        };

    public bool ShouldVisibleRisk => Risk != AppRisk.Unknown;
}
