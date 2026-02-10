using optimizerDuck.Resources.Languages;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.Models.UI;

[Flags]
public enum OptimizationTags
{
    None = 0,

    // Hardware
    Ram       = 1 << 0,
    Display   = 1 << 1,
    Disk      = 1 << 2,

    // Network
    Network   = 1 << 3,
    NetworkRequired = 1 << 4,

    // System & Security
    Privacy   = 1 << 5,
    Security  = 1 << 6,
    System    = 1 << 7,

    // User Experience
    Audio     = 1 << 8,
    Visual    = 1 << 9,

    // GPU Vendors
    Nvidia    = 1 << 10,
    Amd       = 1 << 11,
    Intel     = 1 << 12,

    // Power
    Power     = 1 << 13,

    // Performance
    Performance = 1 << 14,
    Latency     = 1 << 15
}


public static class OptimizationTagsToDisplay
{
    extension(OptimizationTags tags)
    {
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

        private OptimizationTagDisplay ToDisplay()
        {
            return tags switch
            {
                OptimizationTags.Ram => new OptimizationTagDisplay(
                    SymbolRegular.Memory16,
                    Translations.Optimizer_UI_Tags_Ram),

                OptimizationTags.Disk => new OptimizationTagDisplay(
                    SymbolRegular.HardDrive20,
                    Translations.Optimizer_UI_Tags_Disk),

                OptimizationTags.Latency => new OptimizationTagDisplay(
                    SymbolRegular.Clock24,
                    Translations.Optimizer_UI_Tags_Latency),
                
                OptimizationTags.Visual => new OptimizationTagDisplay(
                    SymbolRegular.VideoClip24,
                    Translations.Optimizer_UI_Tags_Visual),
                
                OptimizationTags.Display => new OptimizationTagDisplay(
                    SymbolRegular.VideoClip24,
                    Translations.Optimizer_UI_Tags_Display),

                OptimizationTags.Network => new OptimizationTagDisplay(
                    SymbolRegular.NetworkAdapter16,
                    Translations.Optimizer_UI_Tags_Network),

                OptimizationTags.Performance => new OptimizationTagDisplay(
                    SymbolRegular.Gauge24,
                    Translations.Optimizer_UI_Tags_Performance),

                OptimizationTags.Privacy => new OptimizationTagDisplay(
                    SymbolRegular.LockOpen24,
                    Translations.Optimizer_UI_Tags_Privacy),

                OptimizationTags.Audio => new OptimizationTagDisplay(
                    SymbolRegular.Headphones24,
                    Translations.Optimizer_UI_Tags_Audio),

                OptimizationTags.System => new OptimizationTagDisplay(
                    SymbolRegular.Desktop24,
                    Translations.Optimizer_UI_Tags_System),

                OptimizationTags.Security => new OptimizationTagDisplay(
                    SymbolRegular.LockClosed24,
                    Translations.Optimizer_UI_Tags_Security),

                OptimizationTags.NetworkRequired => new OptimizationTagDisplay(
                    SymbolRegular.NetworkAdapter16,
                    Translations.Optimizer_UI_Tags_NetworkRequired),

                OptimizationTags.Nvidia => new OptimizationTagDisplay(
                    SymbolRegular.VideoClip24,
                    Translations.Optimizer_UI_Tags_Nvidia),

                OptimizationTags.Amd => new OptimizationTagDisplay(
                    SymbolRegular.VideoClip24,
                    Translations.Optimizer_UI_Tags_Amd),

                OptimizationTags.Intel => new OptimizationTagDisplay(
                    SymbolRegular.VideoClip24,
                    Translations.Optimizer_UI_Tags_Intel),

                OptimizationTags.Power => new OptimizationTagDisplay(
                    SymbolRegular.BatteryCharge24,
                    Translations.Optimizer_UI_Tags_Power),

                _ => throw new ArgumentOutOfRangeException(nameof(tags))
            };
        }
    }
}

public readonly record struct OptimizationTagDisplay(SymbolRegular Icon, string Display);