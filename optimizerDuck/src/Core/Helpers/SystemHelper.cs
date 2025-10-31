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

    public static void EnsureDirectoriesExists()
    {
        if (!Directory.Exists(Defaults.RootPath))
        {
            Log.LogInformation(@"AppData\optimizerDuck directory does not exist. Creating directory at: {Path}",
                Defaults.RootPath);
            Directory.CreateDirectory(Defaults.RootPath);
        }

        if (!Directory.Exists(Defaults.ResourcesPath))
        {
            Log.LogInformation("Resources directory does not exist. Creating directory at: {Path}",
                Defaults.ResourcesPath);
            Directory.CreateDirectory(Defaults.ResourcesPath);
        }
    }

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

    public static void Title(string title)
    {
        Console.Title = $"optimizerDuck [{Defaults.FileVersion}] - {title}";
    }

    public static List<string> CheckForExclusions()
    {
        var result = ShellService.PowerShell($$"""
                                               @{ 
                                                   SelfPath = (Get-MpPreference).ExclusionPath -contains "{{Defaults.ExePath}}";
                                                   RootPath = (Get-MpPreference).ExclusionPath -contains "{{Defaults.RootPath}}"
                                               } | ConvertTo-Json
                                               """);
        var exclusions = JsonConvert.DeserializeObject<Dictionary<string, bool>>(result.Stdout);
        var missingPaths = new List<string>();
        if (exclusions != null && exclusions.TryGetValue("SelfPath", out var selfPathExcluded) && !selfPathExcluded)
        {
            Log.LogWarning("Path not excluded: {SelfPath}", Defaults.ExePath);
            missingPaths.Add(Defaults.ExeDir);
        }

        if (exclusions != null && exclusions.TryGetValue("RootPath", out var rootPathExcluded) && !rootPathExcluded)
        {
            Log.LogWarning("Path not excluded: {RootPath}", Defaults.RootPath);
            missingPaths.Add(Defaults.RootPath);
        }

        // return missing path
        return missingPaths;
    }

    public static void AddToExclusions(List<string> paths)
    {
        using var tracker = ServiceTracker.Begin();
        foreach (var path in paths)
        {
            var result = ShellService.PowerShell($"Add-MpPreference -ExclusionPath \"{path}\"");
            if (result.ExitCode != 0) Log.LogWarning($"[{Theme.Error}]Failed to add path to exclusions: {path}[/]");
        }
    }
}