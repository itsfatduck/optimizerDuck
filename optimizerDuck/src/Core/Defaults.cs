using System.Diagnostics;
using optimizerDuck.UI;
using optimizerDuck.UI.Components;
using Spectre.Console;

namespace optimizerDuck.Core;

public static class Defaults
{
    public const string RawLogo = """
                                                _   _           _              _____             _
                                               | | (_)         (_)            |  __ \           | |
                                     ___  _ __ | |_ _ _ __ ___  _ _______ _ __| |  | |_   _  ___| | __
                                    / _ \| '_ \| __| | '_ ` _ \| |_  / _ \ '__| |  | | | | |/ __| |/ /
                                   | (_) | |_) | |_| | | | | | | |/ /  __/ |  | |__| | |_| | (__|   <
                                    \___/| .__/ \__|_|_| |_| |_|_/___\___|_|  |_____/ \__,_|\___|_|\_\
                                         | |
                                         |_|
                                  """;

    public const string
        EscapeCancellableConsoleMultiSelectionPromptInstructionsText // ok
            = $"""
               [grey](Use [{Theme.Primary}]↑ ↓[/] to navigate)[/]
               [grey](Press [{Theme.Info}]<space>[/] to toggle an option, [{Theme.Success}]<enter>[/] to continue)[/]
               [grey][[[{Theme.Error}]CTRL + C[/] or [{Theme.Error}]Escape[/] to go back to previous menu]][/]
               """;

    public const string DiscordInvite = "https://discord.gg/tDUBDCYw9Q";
    public const string GitHubRepo = "https://github.com/itsfatduck/optimizerDuck";

    public const string ZwtDownloadUrl =
        "https://github.com/LuSlower/ZwTimerResolution/releases/download/0.0.0.6/ZwTimer.exe";

    public const string PowerPlanUrl = "https://github.com/itsfatduck/optimizerDuck/raw/refs/heads/master/optimizerDuck.Resources/optimizerDuck.pow";

    public const string RestorePointName = "optimizerDuck Restore Point";
    public const string PowerPlanGUID = "946c0ca5-6ee0-4f2a-9dd7-7addbb8e60f5";

    public static readonly Panel Logo = new(
            new Align(
                // @formatter:off
                new Markup(
                    $"""
                            [{Theme.Secondary}]              _   _           _             [/][{Theme.Accent}] _____             _    [/]
                            [{Theme.Secondary}]             | | (_)         (_)            [/][{Theme.Accent}]|  __ \           | |   [/]
                            [{Theme.Secondary}]   ___  _ __ | |_ _ _ __ ___  _ _______ _ __[/][{Theme.Accent}]| |  | |_   _  ___| | __[/]
                            [{Theme.Secondary}]  / _ \| '_ \| __| | '_ ` _ \| |_  / _ \ '__[/][{Theme.Accent}]| |  | | | | |/ __| |/ /[/]
                            [{Theme.Secondary}] | (_) | |_) | |_| | | | | | | |/ /  __/ |  [/][{Theme.Accent}]| |__| | |_| | (__|   < [/]
                            [{Theme.Secondary}]  \___/| .__/ \__|_|_| |_| |_|_/___\___|_|  [/][{Theme.Accent}]|_____/ \__,_|\___|_|\_\[/]
                            [{Theme.Secondary}]       | |                                  [/]                        
                            [{Theme.Secondary}]       |_|                                  [/]                        
                            """),
                // @formatter:on
                HorizontalAlignment.Center
            ))
        { Border = CustomBoxBorder.UnderlineBorder, Padding = new Padding(0, 1, 0, 1) };

    private static readonly string ExePath = Environment.ProcessPath!;
    public static readonly string ExeDir = Path.GetDirectoryName(ExePath)!;
    public static readonly string FileVersion = FileVersionInfo.GetVersionInfo(ExePath).FileVersion!;

    public static readonly string RootPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "optimizerDuck");

    public static readonly string ResourcesPath =
        Path.Combine(RootPath, "Resources");

    public static readonly Dictionary<string, string> SAFE_APPS = new()
    {
        ["BingWeather"] = "Weather",
        ["GetHelp"] = "Get Help",
        ["Getstarted"] = "Get Started / Tips",
        ["Messaging"] = "Messaging",
        ["Microsoft3DViewer"] = "3D Viewer",
        ["MicrosoftSolitaireCollection"] = "Solitaire Collection",
        ["MicrosoftStickyNotes"] = "Sticky Notes",
        ["MixedReality.Portal"] = "Mixed Reality Portal",
        ["OneConnect"] = "OneConnect (Connect App)",
        ["People"] = "People",
        ["Print3D"] = "Print 3D",
        ["SkypeApp"] = "Skype",
        ["WindowsAlarms"] = "Alarms & Clock",
        ["WindowsCamera"] = "Camera",
        ["WindowsMaps"] = "Maps",
        ["WindowsFeedbackHub"] = "Feedback Hub",
        ["WindowsSoundRecorder"] = "Voice Recorder",
        ["YourPhone"] = "Phone Link",
        ["ZuneMusic"] = "Media Player / Groove",
        ["Sway"] = "Microsoft Sway",
        ["3dBuilder"] = "3D Builder (legacy)",
        ["bing"] = "Microsoft Bing (everything)",
        ["Drawboard PDF"] = "Drawboard PDF (OEM)",
        ["WindowsPhone"] = "Windows Phone (old)",
        ["CommsPhone"] = "Communications Phone (old)",
        ["Microsoft.Todos"] = "Microsoft To Do",
        ["windowscommunicationsapps"] = "Mail and Calendar",
        ["MicrosoftCorporationII.QuickAssist"] = "Quick Assist"
    };

    public static readonly Dictionary<string, string> CAUTION_APPS = new()
    {
        ["WindowsCalculator"] = "Calculator",
        ["Microsoft.Windows.Photos"] = "Photos",
        ["Microsoft.MSPaint"] = "Paint",
        ["Microsoft.Paint3D"] = "Paint 3D",
        ["Microsoft.ScreenSketch"] = "Snip & Sketch",
        ["MicrosoftWindows.Client.SnippingTool"] = "Snipping Tool",
        ["Microsoft.WindowsStore"] = "Microsoft Store",
        ["Microsoft.XboxIdentityProvider"] = "Xbox Identity Provider",
        ["Microsoft.WindowsTerminal"] = "Windows Terminal",
        ["Clipchamp.Clipchamp"] = "Clipchamp (Video Editor)",
        ["MicrosoftWindows.Notepad"] = "Notepad",
        // Extensions
        ["HEIFImageExtension"] = "HEIF Image Extension",
        ["WebMediaExtensions"] = "Web Media Extensions",
        ["WebpImageExtension"] = "WebP Image Extension",
        // Xbox / Game Bar
        ["Microsoft.XboxGamingOverlay"] = "Xbox Game Bar",
        ["Microsoft.XboxApp"] = "Xbox App (Console Companion)",
        ["Microsoft.Xbox.TCUI"] = "Xbox TCUI"
    };
}