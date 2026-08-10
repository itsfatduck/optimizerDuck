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
        "Microsoft.News", // Microsoft News
        "MicrosoftStart", // Microsoft Start (Win11 22H2+)
        "Microsoft.BingFinance",
        "Microsoft.BingSports",
        "Microsoft.BingFoodAndDrink",
        "Microsoft.BingHealthAndFitness",
        "Microsoft.BingTravel",
        "Microsoft.BingTranslator", // Win10
        // Help, Tips & Feedback
        "Microsoft.GetStarted",
        "Microsoft.WindowsTips",
        "Microsoft.WindowsFeedbackHub",
        // Communications (Legacy/Discontinued)
        "Microsoft.Messaging",
        "Microsoft.OneConnect",
        "Microsoft.People",
        "Microsoft.SkypeApp",
        "MicrosoftTeams", // Old Teams (personal)
        "MSTeams", // New Teams
        // Office (Web Wrappers & Legacy)
        "Microsoft.OneNote",
        "Microsoft.Todos",
        "Microsoft.MicrosoftOfficeHub",
        "Microsoft.Office.OneNote",
        "Microsoft.Office.Sway",
        "Microsoft.MicrosoftJournal",
        "Microsoft.MicrosoftPowerBIForWindows", // Power BI
        // Social & Wallet (Legacy)
        "Microsoft.Wallet",
        "Microsoft.MSWallet",
        // Mixed Reality & 3D (Legacy, not in Win11 images)
        "Microsoft.Microsoft3DViewer",
        "Microsoft.MixedReality.Portal",
        "Microsoft.Print3D",
        "Microsoft.3DBuilder",
        "Microsoft.Paint3D", // Paint 3D (Win10 only)
        // Multimedia & Entertainment
        "Microsoft.ZuneVideo",
        "Microsoft.WindowsSoundRecorder",
        "Microsoft.StickyNotes",
        "Microsoft.MicrosoftStickyNotes",
        "Microsoft.MicrosoftSolitaireCollection",
        "Microsoft.WindowsAlarms",
        "Microsoft.549981C3F5F10", // Cortana
        // Legacy & Redundant Utilities
        "Microsoft.Windows.DevHome",
        "Microsoft.WindowsMaps",
        "Microsoft.XboxApp", // Old Xbox Console Companion (Discontinued)
        "Microsoft.Windows.AIHub", // Copilot+ AI Hub (Copilot+ PCs)
        "Microsoft.MinecraftUWP", // Minecraft (Win10 consumer images)
        "Microsoft.NetworkSpeedTest", // Win10
        "Drawboard.DrawboardPDF",
        // Third-party (commonly pre-installed on OEM machines)
        "SpotifyAB.SpotifyMusic",
        "Clipchamp.Clipchamp",
        "Disney.37853FC22B2CE",
        "Amazon.com.Amazon",
        "4DF9E0F8.Netflix",
        "BytedancePte.Ltd.TikTok",
        "king.com.CandyCrushSaga",
        "king.com.CandyCrushSodaSaga",
        "king.com.BubbleWitch3Saga",
        "AmazonVideo.PrimeVideo",
        "Facebook.Instagram",
        "FACEBOOK.FACEBOOK",
        "7EE7776C.LinkedInforWindows",
        "Flipboard",
        "iHeartRadio",
        "PandoraMediaInc",
        "TuneInRadio",
        "Duolingo-LearnLanguagesforFree",
        "PicsArt-PhotoStudio",
        "WinZipUniversal",
        "AdobeSystemsIncorporated.AdobePhotoshopExpress",
        "OneCalendar",
        "NYTCrossword",
        "MarchofEmpires",
        "flaregamesGmbH.RoyalRevolt",
        "Asphalt8Airborne",
        "FarmVille2CountryEscape",
        "HiddenCity",
        "CaesarsSlotsFreeCasino",
        "COOKINGFEVER",
        "DisneyMagicKingdoms",
        "PhototasticCollage",
        "AutodeskSketchBook",
        "PolarrPhotoEditorAcademicEdition",
        "Sidia.LiveWallpaper",
        "HULULLC.HULUPLUS",
        "SlingTV",
        "DolbyLaboratories.DolbyAccess", // OEM audio app
    };

    public static readonly HashSet<string> CautionApps = new()
    {
        // Core Utilities (default handlers for common file types)
        "Microsoft.WindowsCalculator",
        "Microsoft.WindowsPhotos",
        "Microsoft.MSPaint", // Classic Paint (Win10)
        "Microsoft.Paint",
        "Microsoft.WindowsNotepad",
        "Microsoft.WindowsCamera",
        "Microsoft.ScreenSketch", // Snipping Tool
        // Default browser (tied to WebView2-based apps and Windows features)
        "Microsoft.MicrosoftEdge",
        "Microsoft.MicrosoftEdge.Stable",
        // System Tools
        "Microsoft.WindowsStore",
        "Microsoft.StorePurchaseApp",
        "Microsoft.DesktopAppInstaller", // winget
        "Microsoft.WindowsTerminal",
        "Microsoft.WindowsTerminalPreview",
        "Microsoft.RemoteDesktop",
        "Microsoft.PowerAutomateDesktop",
        // Communication & Collaboration
        "Microsoft.WindowsCommunicationsApps", // Mail & Calendar
        "Microsoft.OutlookForWindows", // New Outlook
        "Microsoft.YourPhone", // Phone Link
        "MicrosoftWindows.CrossDevice", // Phone Link features in Explorer
        // Widgets & Search integration
        "Microsoft.StartExperiencesApp",
        "MicrosoftWindows.Client.WebExperience",
        "Microsoft.WidgetsPlatformRuntime",
        "Microsoft.BingSearch",
        // Default media handler
        "Microsoft.ZuneMusic", // Media Player
        // Security, Family & Support
        "MicrosoftCorporationII.MicrosoftFamily", // parental controls
        "MicrosoftCorporationII.MicrosoftSupportDiagnosticTool",
        "MicrosoftCorporationII.QuickAssist",
        "Microsoft.GetHelp", // Required by some Windows 11 Troubleshooters
        // Cloud & Productivity
        "Microsoft.OneDrive",
        "Microsoft.Whiteboard",
        "Microsoft.M365Companions",
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
        "Microsoft.Xbox.TCUI",
        "Microsoft.GamingApp",
        // OEM Software (drivers, updates & support from the hardware vendor)
        "AD2F1837.HPAIExperienceCenter",
        "AD2F1837.HPConnectedMusic",
        "AD2F1837.HPConnectedPhotopoweredbySnapfish",
        "AD2F1837.HPDesktopSupportUtilities",
        "AD2F1837.HPEasyClean",
        "AD2F1837.HPFileViewer",
        "AD2F1837.HPJumpStarts",
        "AD2F1837.HPPCHardwareDiagnosticsWindows",
        "AD2F1837.HPPowerManager",
        "AD2F1837.HPPrinterControl",
        "AD2F1837.HPPrivacySettings",
        "AD2F1837.HPQuickDrop",
        "AD2F1837.HPQuickTouch",
        "AD2F1837.HPRegistration",
        "AD2F1837.HPSupportAssistant",
        "AD2F1837.HPSureShieldAI",
        "AD2F1837.HPSystemInformation",
        "AD2F1837.HPWelcome",
        "AD2F1837.HPWorkWell",
        "AD2F1837.myHP",
        "DellInc.DellSupportAssistforPCs",
        "DellInc.DellDigitalDelivery",
        "DellInc.DellMobileConnect",
        "E046963F.LenovoCompanion",
        "LenovoCompanyLimited.LenovoVantageService",
        "LGElectronics.LGMonitorApp",
        "EclipseManager", // Dell OEM
        "ACGMediaPlayer", // OEM media player
        "ActiproSoftwareLLC", // OEM-bundled UI components
        "CyberLinkMediaSuiteEssentials", // OEM media suite
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

    public static bool IsWindows11OrGreater =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);
}
