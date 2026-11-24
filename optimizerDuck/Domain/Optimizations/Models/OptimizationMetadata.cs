using optimizerDuck.Services.Configuration;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Optimizations.Models;

/// <summary>System aspects affected by an optimization.</summary>
public enum ImpactArea
{
    Performance,
    Latency,
    Memory,
    Storage,
    Power,
    Privacy,
    Security,
    Network,
    Stability,
    Clutter,
    Features,
}

/// <summary>Direction of change for a given <see cref="ImpactArea"/>.</summary>
public enum ImpactDirection
{
    Improves,
    TradeOff,
}

/// <summary>One structured impact entry declared by an optimization.</summary>
public sealed record OptimizationImpact(ImpactArea Area, ImpactDirection Direction);

/// <summary>Source category for an optimization's reference documentation.</summary>
public enum SourceKind
{
    MicrosoftDocs,
    Benchmark,
    Reference,
}

/// <summary>Verifiable reference source supporting an optimization or its tradeoff.</summary>
public sealed record OptimizationSource(string Title, string Url, SourceKind Kind)
{
    /// <summary>
    ///     Creates an <see cref="OptimizationSource"/> and infers <see cref="SourceKind"/> from
    ///     the host when <paramref name="kind"/> is omitted.
    /// </summary>
    public static OptimizationSource From(string title, string url, SourceKind? kind = null)
    {
        var resolvedKind =
            kind
            ?? (
                Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (
                    uri.Host.Equals("learn.microsoft.com", StringComparison.OrdinalIgnoreCase)
                    || uri.Host.Equals("support.microsoft.com", StringComparison.OrdinalIgnoreCase)
                )
                    ? SourceKind.MicrosoftDocs
                    : SourceKind.Reference
            );

        return new OptimizationSource(title, url, resolvedKind);
    }

    /// <summary>Returns the host label shown next to the link title.</summary>
    public string Host =>
        Uri.TryCreate(Url, UriKind.Absolute, out var uri) ? uri.Host : string.Empty;

    /// <summary>Gets the Fluent icon matching <see cref="Kind"/>.</summary>
    public SymbolRegular Icon =>
        Kind switch
        {
            SourceKind.MicrosoftDocs => SymbolRegular.DocumentText24,
            SourceKind.Benchmark => SymbolRegular.Gauge24,
            _ => SymbolRegular.Globe24,
        };
}

/// <summary>Restart action required for an optimization to take full effect.</summary>
public enum RestartRequirement
{
    None,
    Explorer,
    SignOut,
    Reboot,
}

/// <summary>Structured metadata describing impacts, restart needs, and sources.</summary>
public sealed record OptimizationMetadata
{
    public static readonly OptimizationMetadata Empty = new();

    public IReadOnlyList<OptimizationImpact> Impacts { get; init; } = [];
    public IReadOnlyList<OptimizationSource> Sources { get; init; } = [];
    public RestartRequirement RequiresRestart { get; init; } = RestartRequirement.None;
    public bool SetsGroupPolicy { get; init; }
    public bool ConfirmBeforeApply { get; init; }
}

/// <summary>Localized display projection for one <see cref="OptimizationImpact"/>.</summary>
public readonly record struct OptimizationImpactDisplay
{
    public required SymbolRegular Icon { get; init; }
    public required string Label { get; init; }
    public required ImpactDirection Direction { get; init; }
    public bool IsTradeOff => Direction == ImpactDirection.TradeOff;
}

/// <summary>Display helpers for <see cref="OptimizationImpact"/> and <see cref="RestartRequirement"/>.</summary>
public static class OptimizationMetadataExtensions
{
    /// <summary>Projects a list of impacts into localized display chips.</summary>
    public static IReadOnlyList<OptimizationImpactDisplay> ToDisplays(
        this IReadOnlyList<OptimizationImpact> impacts
    )
    {
        if (impacts.Count == 0)
            return [];

        return
        [
            .. impacts.Select(impact => new OptimizationImpactDisplay
            {
                Icon = impact.Area.ToIcon(),
                Label = Loc.Instance[$"Optimizer.UI.Impact.{impact.Area}.{impact.Direction}"],
                Direction = impact.Direction,
            }),
        ];
    }

    /// <summary>Returns the localized label for a restart requirement, or empty for <see cref="RestartRequirement.None"/>.</summary>
    public static string ToDisplay(this RestartRequirement requirement) =>
        requirement switch
        {
            RestartRequirement.Explorer => Loc.Instance["Optimizer.UI.Restart.Explorer"],
            RestartRequirement.SignOut => Loc.Instance["Optimizer.UI.Restart.SignOut"],
            RestartRequirement.Reboot => Loc.Instance["Optimizer.UI.Restart.Reboot"],
            _ => string.Empty,
        };

    private static SymbolRegular ToIcon(this ImpactArea area) =>
        area switch
        {
            ImpactArea.Performance => SymbolRegular.Gauge24,
            ImpactArea.Latency => SymbolRegular.Clock24,
            ImpactArea.Memory => SymbolRegular.Memory16,
            ImpactArea.Storage => SymbolRegular.HardDrive20,
            ImpactArea.Power => SymbolRegular.BatteryCharge24,
            ImpactArea.Privacy => SymbolRegular.LockClosed24,
            ImpactArea.Security => SymbolRegular.ShieldCheckmark24,
            ImpactArea.Network => SymbolRegular.NetworkAdapter16,
            ImpactArea.Stability => SymbolRegular.Shield24,
            ImpactArea.Clutter => SymbolRegular.Window16,
            ImpactArea.Features => SymbolRegular.Options24,
            _ => SymbolRegular.Info24,
        };
}
