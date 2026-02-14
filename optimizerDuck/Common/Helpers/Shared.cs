using System.Diagnostics;
using System.IO;

namespace optimizerDuck.Common.Helpers;

public static class Shared
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


    public const string DiscordInviteURL = "https://discord.gg/GqvJYQsgSm";
    public const string GitHubRepoURL = "https://github.com/itsfatduck/optimizerDuck";
    public const string SupportMeURL = "https://github.com/itsfatduck/optimizerDuck/blob/master/SUPPORT.md";

    public const string AcknowledgementsURL =
        "https://github.com/itsfatduck/optimizerDuck/blob/master/THIRD-PARTY-NOTICES.md";

    public const string RestorePointName = "optimizerDuck Restore Point";

    public const string PowerPlanUrl =
        "https://github.com/itsfatduck/optimizerDuck/raw/refs/heads/master/optimizerDuck.Resources/optimizerDuck.pow";

    public const string PowerPlanGUID = "8ae61178-2c55-43f2-afb2-f83725823657";

    public static readonly string ExePath = Environment.ProcessPath!;
    public static readonly string ExeDir = Path.GetDirectoryName(ExePath)!;
    public static readonly string ExeName = Path.GetFileName(ExePath);
    public static readonly string FileVersion = FileVersionInfo.GetVersionInfo(ExePath).FileVersion!;

    public static string RootDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "optimizerDuck");

    public static string ResourcesDirectory =>
        Path.Combine(RootDirectory, "Resources");

    public static string RevertDirectory =>
        Path.Combine(RootDirectory, "Revert");
    
    public static readonly Dictionary<string, string> SafeApps = new()
    {
        // bing
        ["Microsoft.BingWeather"] = "Weather",
        ["Microsoft.BingNews"] = "Bing News",
        ["Microsoft.BingSearch"] = "Bing Search",

        ["Microsoft.GetHelp"] = "Get Help",
        ["Microsoft.GetStarted"] = "Get Started / Tips",
        ["Microsoft.Messaging"] = "Messaging",
        ["Microsoft.Microsoft3DViewer"] = "3D Viewer",
        ["Microsoft.MicrosoftSolitaireCollection"] = "Solitaire Collection",
        ["Microsoft.MicrosoftStickyNotes"] = "Sticky Notes",
        ["Microsoft.MixedReality.Portal"] = "Mixed Reality Portal",
        ["Microsoft.OneConnect"] = "OneConnect (Connect App)",
        ["Microsoft.People"] = "People",
        ["Microsoft.Print3D"] = "Print 3D",
        ["SkypeApp"] = "Skype",
        ["Microsoft.WindowsAlarms"] = "Alarms & Clock",
        ["Microsoft.WindowsCamera"] = "Camera",
        ["Microsoft.WindowsMaps"] = "Maps",
        ["Microsoft.WindowsFeedbackHub"] = "Feedback Hub",
        ["Microsoft.WindowsSoundRecorder"] = "Voice Recorder",
        ["Microsoft.ZuneMusic"] = "Media Player / Groove",
        ["Microsoft.Sway"] = "Microsoft Sway",
        ["Microsoft.3DBuilder"] = "3D Builder (legacy)",
        ["Microsoft.DrawboardPDF"] = "Drawboard PDF (OEM)",
        ["WindowsPhone"] = "Windows Phone (old)",
        ["CommsPhone"] = "Communications Phone (old)",
        ["Microsoft.Todos"] = "Microsoft To Do",
        ["Microsoft.WindowsCommunicationsApps"] = "Mail and Calendar",
        ["MicrosoftCorporationII.QuickAssist"] = "Quick Assist",
        ["Microsoft.Office.OneNote"] = "OneNote",
        ["Microsoft.YourPhone"] = "Phone Link",
        ["Microsoft.MicrosoftNews"] = "Microsoft News",
        ["Microsoft.MicrosoftOfficeHub"] = "Office Hub"
    };


    public static readonly Dictionary<string, string> CautionApps = new()
    {
        ["Microsoft.WindowsCalculator"] = "Calculator",
        ["Microsoft.Windows.Photos"] = "Photos",
        ["Microsoft.MSPaint"] = "Paint",
        ["Microsoft.Paint3D"] = "Paint 3D",
        ["Microsoft.ScreenSketch"] = "Snipping Tool & Sketch",
        ["Microsoft.WindowsStore"] = "Microsoft Store",
        ["Microsoft.XboxIdentityProvider"] = "Xbox Identity Provider",
        ["Microsoft.WindowsTerminal"] = "Windows Terminal",
        ["Microsoft.WindowsTerminalPreview"] = "Windows Terminal (Preview)",
        ["Clipchamp.Clipchamp"] = "Clipchamp (Video Editor)",
        ["Microsoft.WindowsNotepad"] = "Notepad",
        ["Microsoft.PowerAutomateDesktop"] = "Power Automate",
        ["MicrosoftTeams"] = "Microsoft Teams",
        ["MicrosoftCorporationII.MicrosoftFamily"] = "Microsoft Family Safety",
        ["MicrosoftCorporationII.MicrosoftSupportDiagnosticTool"] = "Support Diagnostic Tool",
        // Extensions
        ["Microsoft.HEIFImageExtension"] = "HEIF Image Extension",
        ["Microsoft.WebMediaExtensions"] = "Web Media Extensions",
        ["Microsoft.WebpImageExtension"] = "WebP Image Extension",
        ["Microsoft.RawImageExtension"] = "Raw Image Extension",
        ["Microsoft.VP9VideoExtensions"] = "VP9 Video Extension",
        ["Microsoft.AV1VideoExtension"] = "AV1 Video Extension",
        // Xbox / Game Bar
        ["Microsoft.XboxSpeechToTextOverlay"] = "Xbox Speech Overlay",
        ["Microsoft.XboxGameOverlay"] = "Xbox Game Overlay",
        ["Microsoft.XboxGamingOverlay"] = "Xbox Game Bar",
        ["Microsoft.XboxApp"] = "Xbox Console Companion",
        ["Microsoft.Xbox.TCUI"] = "Xbox TCUI"
    };
}