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

    public const string DiscordInviteURL = "https://discord.gg/tDUBDCYw9Q";
    public const string WebsiteURL = "https://optimizerduck.vercel.app/";
    public const string GitHubRepoURL = "https://github.com/itsfatduck/optimizerDuck";
    public const string CommunityURL = "https://optimizerduck.vercel.app/docs/community";
    public const string ContributeURL = "https://optimizerduck.vercel.app/docs/contribute/overview";

    public const string AcknowledgementsURL =
        "https://github.com/itsfatduck/optimizerDuck/blob/master/THIRD-PARTY-NOTICES.md";

    public const string RestorePointName = "optimizerDuck Restore Point";

    public const string PowerPlanUrl =
        "https://github.com/itsfatduck/optimizerDuck/raw/refs/heads/master/optimizerDuck.Resources/optimizerDuck.pow";

    public const string PowerPlanGUID = "8ae61178-2c55-43f2-afb2-f83725823657";

    public static readonly string ExePath = Environment.ProcessPath!;
    public static readonly string ExeDir = Path.GetDirectoryName(ExePath)!;
    public static readonly string ExeName = Path.GetFileName(ExePath);
    public static readonly string FileVersion = FileVersionInfo
        .GetVersionInfo(ExePath)
        .FileVersion!;

    public static readonly HashSet<string> SafeApps = new()
    {
        // Bing / MSN / News
        "Microsoft.BingWeather",
        "Microsoft.BingNews",
        "Microsoft.BingSearch",
        "Microsoft.BingFinance",
        "Microsoft.BingSports",
        "Microsoft.BingFoodAndDrink",
        "Microsoft.BingHealthAndFitness",
        "Microsoft.BingTravel",
        "Microsoft.MicrosoftNews",
        // Help, Tips & Feedback
        "Microsoft.GetHelp",
        "Microsoft.GetStarted",
        "Microsoft.WindowsFeedbackHub",
        "Microsoft.WindowsTips",
        "MicrosoftCorporationII.QuickAssist",
        // Communications (Legacy/Web Wrappers)
        "Microsoft.Messaging",
        "Microsoft.OneConnect",
        "Microsoft.People",
        "Microsoft.YourPhone",
        "Microsoft.SkypeApp",
        "MicrosoftTeams",
        // Office (Web Wrappers & Legacy)
        "Microsoft.OneNote",
        "Microsoft.Todos",
        "Microsoft.MicrosoftOfficeHub",
        "Microsoft.Office.OneNote",
        "Microsoft.Office.Sway",
        "Microsoft.OutlookForWindows",
        "Microsoft.MicrosoftJournal",
        // Social & Wallet
        "Microsoft.Wallet",
        "Microsoft.MSWallet",
        "Microsoft.LinkedInForWindows",
        // Mixed Reality & 3D
        "Microsoft.Microsoft3DViewer",
        "Microsoft.MixedReality.Portal",
        "Microsoft.Print3D",
        "Microsoft.3DBuilder",
        "Microsoft.Paint3D",
        // Multimedia (Legacy/Redundant)
        "Microsoft.ZuneMusic",
        "Microsoft.ZuneVideo",
        "Microsoft.Sway",
        "Microsoft.WindowsSoundRecorder",
        "Microsoft.StickyNotes",
        "Microsoft.MicrosoftStickyNotes",
        // Entertainment, Games & Widgets
        "Microsoft.MicrosoftSolitaireCollection",
        "Microsoft.WindowsAlarms",
        "Microsoft.549981C3F5F10", // Cortana
        "Microsoft.Windows.DevHome",
        "Microsoft.StartExperiencesApp",
        // Third-party (often pre-installed by MS)
        "SpotifyAB.SpotifyMusic",
        "Disney.DisneyPlus",
        "Amazon.Amazon",
        "Clipchamp.Clipchamp",
        // Legacy / Others
        "WindowsPhone",
        "CommsPhone",
        "Microsoft.DrawboardPDF",
        // Windows 11 Widgets
        "MicrosoftWindows.Client.WebExperience",
    };

    public static readonly HashSet<string> CautionApps = new()
    {
        // Core Utilities
        "Microsoft.WindowsCalculator",
        "Microsoft.WindowsPhotos",
        "Microsoft.MSPaint",
        "Microsoft.Paint",
        "Microsoft.WindowsNotepad",
        "Microsoft.WindowsCamera",
        "Microsoft.ScreenSketch",
        "Microsoft.WindowsMaps",
        // System Tools
        "Microsoft.WindowsStore",
        "Microsoft.StorePurchaseApp",
        "Microsoft.DesktopAppInstaller",
        "Microsoft.WindowsTerminal",
        "Microsoft.WindowsTerminalPreview",
        "Microsoft.RemoteDesktop",
        "Microsoft.PowerAutomateDesktop",
        // Communication & Collaboration
        "Microsoft.WindowsCommunicationsApps",
        // Security & Family
        "MicrosoftCorporationII.MicrosoftFamily",
        "MicrosoftCorporationII.MicrosoftSupportDiagnosticTool",
        // Media Extensions (Essential for file format support)
        "Microsoft.HEIFImageExtension",
        "Microsoft.WebMediaExtensions",
        "Microsoft.WebpImageExtension",
        "Microsoft.RawImageExtension",
        "Microsoft.VP9VideoExtensions",
        "Microsoft.AV1VideoExtension",
        "Microsoft.HEVCVideoExtension",
        "Microsoft.MPEG2VideoExtension",
        // Xbox / Gaming Services
        "Microsoft.XboxIdentityProvider",
        "Microsoft.XboxSpeechToTextOverlay",
        "Microsoft.XboxGameOverlay",
        "Microsoft.XboxGamingOverlay",
        "Microsoft.XboxApp",
        "Microsoft.Xbox.TCUI",
        "Microsoft.GamingApp",
    };

    public static string RootDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "optimizerDuck"
        );

    public static string ResourcesDirectory => Path.Combine(RootDirectory, "Resources");
    public static string DownloadsDirectory => Path.Combine(ResourcesDirectory, "Downloads");
    public static string AssetsDirectory => Path.Combine(ResourcesDirectory, "Assets");
    public static string RevertDirectory => Path.Combine(RootDirectory, "Revert");
}
