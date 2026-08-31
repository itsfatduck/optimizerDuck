using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.UI;

/// <summary>
///     Flags that categorize an optimization by the system areas it affects.
/// </summary>
[Flags]
public enum OptimizationTags
{
    None = 0,

    // System & Security (highest priority)
    Security = 1 << 6,
    Privacy = 1 << 5,
    System = 1 << 7,

    // Performance
    Performance = 1 << 14,
    Latency = 1 << 15,

    // Hardware
    Disk = 1 << 2,
    Ram = 1 << 0,
    Display = 1 << 1,

    // GPU Vendors
    Nvidia = 1 << 10,
    Amd = 1 << 11,
    Intel = 1 << 12,

    // Power
    Power = 1 << 13,

    // Network
    Network = 1 << 3,
    NetworkRequired = 1 << 4,

    // User Experience
    Audio = 1 << 8,
    Visual = 1 << 9,

    // Platform (lowest priority)
    Windows10Only = 1 << 16,
    Windows11Only = 1 << 17,
}

/// <summary>
///     Provides extension methods to convert <see cref="OptimizationTags" /> to display-friendly representations.
/// </summary>
public static class OptimizationTagsToDisplay
{
    extension(OptimizationTags tags)
    {
        /// <summary>
        ///     Converts the tag flags into a sequence of display-friendly representations.
        /// </summary>
        /// <returns>An enumerable of <see cref="OptimizationTagDisplay" /> for each set flag.</returns>
        public IEnumerable<OptimizationTagDisplay> ToDisplays()
        {
            foreach (var flag in Enum.GetValues<OptimizationTags>())
            {
                if (flag == OptimizationTags.None)
                    continue;

                if (tags.HasFlag(flag))
                    yield return flag.ToDisplay();
            }
        }

        /// <summary>
        ///     Converts a single tag flag to its display representation.
        /// </summary>
        private OptimizationTagDisplay ToDisplay()
        {
            return tags switch
            {
                OptimizationTags.Security => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.LockClosed24,
                    Display = Loc.Instance["Optimizer.UI.Tags.Security"],
                },

                OptimizationTags.Privacy => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.LockOpen24,
                    Display = Loc.Instance["Optimizer.UI.Tags.Privacy"],
                },

                OptimizationTags.System => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.Desktop24,
                    Display = Loc.Instance["Optimizer.UI.Tags.System"],
                },

                OptimizationTags.Performance => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.Gauge24,
                    Display = Loc.Instance["Optimizer.UI.Tags.Performance"],
                },

                OptimizationTags.Latency => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.Clock24,
                    Display = Loc.Instance["Optimizer.UI.Tags.Latency"],
                },

                OptimizationTags.Disk => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.HardDrive20,
                    Display = Loc.Instance["Optimizer.UI.Tags.Disk"],
                },

                OptimizationTags.Ram => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.Memory16,
                    Display = Loc.Instance["Optimizer.UI.Tags.Ram"],
                },

                OptimizationTags.Display => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.VideoClip24,
                    Display = Loc.Instance["Optimizer.UI.Tags.Display"],
                },

                OptimizationTags.Nvidia => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.VideoClip24,
                    Display = Loc.Instance["Optimizer.UI.Tags.Nvidia"],
                },

                OptimizationTags.Amd => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.VideoClip24,
                    Display = Loc.Instance["Optimizer.UI.Tags.Amd"],
                },

                OptimizationTags.Intel => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.VideoClip24,
                    Display = Loc.Instance["Optimizer.UI.Tags.Intel"],
                },

                OptimizationTags.Power => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.BatteryCharge24,
                    Display = Loc.Instance["Optimizer.UI.Tags.Power"],
                },

                OptimizationTags.Network => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.NetworkAdapter16,
                    Display = Loc.Instance["Optimizer.UI.Tags.Network"],
                },

                OptimizationTags.NetworkRequired => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.Wifi120,
                    Display = Loc.Instance["Optimizer.UI.Tags.NetworkRequired"],
                },

                OptimizationTags.Audio => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.Headphones24,
                    Display = Loc.Instance["Optimizer.UI.Tags.Audio"],
                },

                OptimizationTags.Visual => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.VideoClip24,
                    Display = Loc.Instance["Optimizer.UI.Tags.Visual"],
                },

                OptimizationTags.Windows10Only => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.Window16,
                    Display = Loc.Instance["Optimizer.UI.Tags.Windows10Only"],
                },

                OptimizationTags.Windows11Only => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.Window16,
                    Display = Loc.Instance["Optimizer.UI.Tags.Windows11Only"],
                },

                _ => throw new ArgumentOutOfRangeException(nameof(tags)),
            };
        }
    }
}

/// <summary>
///     Represents the UI display data for an optimization tag.
/// </summary>
public readonly record struct OptimizationTagDisplay
{
    /// <summary>
    ///     The icon symbol to display.
    /// </summary>
    public required SymbolRegular Icon { get; init; }

    /// <summary>
    ///     The localized display text for the tag.
    /// </summary>
    public required string Display { get; init; }
}
