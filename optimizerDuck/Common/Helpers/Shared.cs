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
    
    public const string AcknowledgementsURL = "https://github.com/itsfatduck/optimizerDuck/blob/master/THIRD-PARTY-NOTICES.md";
    
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

}
