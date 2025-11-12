using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using optimizerDuck.Core.Services;
using optimizerDuck.UI;
using optimizerDuck.UI.Logger;
using System.Diagnostics;

namespace optimizerDuck.Core.Helpers;

public static class SystemHelper
{
    private static readonly ILogger Log = Logger.CreateLogger(typeof(SystemHelper));

    public static void OpenLogFile()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = Logger.LogFilePath,
            UseShellExecute = true
        });
    }

    public static void OpenWebsite(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    public static bool IsWindows11OrGreater()
    {
        return int.TryParse(SystemInfoService.Snapshot.Os.Version, out var v) && v >= 11;
    }

    public static void Title(string title)
    {
        Console.Title = $"optimizerDuck [{Defaults.FileVersion}] - {title}";
    }

    public static List<string> CheckForExclusions()
    {
        var exePath = Defaults.ExePath;
        var rootPath = Defaults.RootPath;

        var result = ShellService.PowerShell($$"""
                                               @{ 
                                                   SelfPath = (Get-MpPreference).ExclusionPath -contains "{{exePath}}";
                                                   RootPath = (Get-MpPreference).ExclusionPath -contains "{{rootPath}}"
                                               } | ConvertTo-Json
                                               """);
        var exclusions = JsonConvert.DeserializeObject<Dictionary<string, bool>>(result.Stdout);
        var missingPaths = new List<string>();
        if (exclusions != null && exclusions.TryGetValue("SelfPath", out var selfPathExcluded) && !selfPathExcluded)
        {
            Log.LogWarning("Path not excluded: {SelfPath}", exePath);
            missingPaths.Add(exePath);
        }

        if (exclusions != null && exclusions.TryGetValue("RootPath", out var rootPathExcluded) && !rootPathExcluded)
        {
            Log.LogWarning("Path not excluded: {RootPath}", rootPath);
            missingPaths.Add(rootPath);
        }
        return missingPaths;
    }

    public static void AddToExclusions(List<string> paths)
    {
        using var tracker = ServiceTracker.Begin(Log);
        foreach (var path in paths)
        {
            var result = ShellService.PowerShell($"Add-MpPreference -ExclusionPath \"{path}\"");
            if (result.ExitCode != 0) Log.LogError($"[{Theme.Error}]Failed to add path to exclusions: {path}[/]");
        }
    }
}