using optimizerDuck.Resources.Languages;
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

    // Power
    Power = 1 << 13,

    // Network
    Network = 1 << 3,
    NetworkRequired = 1 << 4,

    // GPU Vendors
    Nvidia = 1 << 10,
    Amd = 1 << 11,
    Intel = 1 << 12,

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
                    Display = Translations.Optimizer_UI_Tags_Security,
                },

                OptimizationTags.Privacy => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.LockOpen24,
                    Display = Translations.Optimizer_UI_Tags_Privacy,
                },

                OptimizationTags.System => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.Desktop24,
                    Display = Translations.Optimizer_UI_Tags_System,
                },

                OptimizationTags.Performance => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.Gauge24,
                    Display = Translations.Optimizer_UI_Tags_Performance,
                },

                OptimizationTags.Latency => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.Clock24,
                    Display = Translations.Optimizer_UI_Tags_Latency,
                },

                OptimizationTags.Disk => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.HardDrive20,
                    Display = Translations.Optimizer_UI_Tags_Disk,
                },

                OptimizationTags.Ram => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.Memory16,
                    Display = Translations.Optimizer_UI_Tags_Ram,
                },

                OptimizationTags.Display => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.VideoClip24,
                    Display = Translations.Optimizer_UI_Tags_Display,
                },

                OptimizationTags.Power => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.BatteryCharge24,
                    Display = Translations.Optimizer_UI_Tags_Power,
                },

                OptimizationTags.Network => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.NetworkAdapter16,
                    Display = Translations.Optimizer_UI_Tags_Network,
                },

                OptimizationTags.NetworkRequired => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.Wifi120,
                    Display = Translations.Optimizer_UI_Tags_NetworkRequired,
                },

                OptimizationTags.Nvidia => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.VideoClip24,
                    Display = Translations.Optimizer_UI_Tags_Nvidia,
                },

                OptimizationTags.Amd => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.VideoClip24,
                    Display = Translations.Optimizer_UI_Tags_Amd,
                },

                OptimizationTags.Intel => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.VideoClip24,
                    Display = Translations.Optimizer_UI_Tags_Intel,
                },

                OptimizationTags.Audio => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.Headphones24,
                    Display = Translations.Optimizer_UI_Tags_Audio,
                },

                OptimizationTags.Visual => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.VideoClip24,
                    Display = Translations.Optimizer_UI_Tags_Visual,
                },

                OptimizationTags.Windows10Only => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.Window16,
                    Display = Translations.Optimizer_UI_Tags_Windows10Only,
                },

                OptimizationTags.Windows11Only => new OptimizationTagDisplay
                {
                    Icon = SymbolRegular.Window16,
                    Display = Translations.Optimizer_UI_Tags_Windows11Only,
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
