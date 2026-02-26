using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using optimizerDuck.Core.Models.StartupManager;
using optimizerDuck.Services.OptimizationServices;

namespace optimizerDuck.Services;

public class StartupManagerService(ILogger<StartupManagerService> logger)
{
    public async Task<List<StartupApp>> GetStartupAppsAsync()
    {
        var apps = new List<StartupApp>();

        await Task.Run(() =>
        {
            // 1. Registry
            ScanRegistryKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run",
                StartupAppLocation.RegistryHKCURun, apps);
            ScanRegistryKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run",
                StartupAppLocation.RegistryHKLMRun, apps);
            ScanRegistryKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\RunOnce",
                StartupAppLocation.RegistryHKCURunOnce, apps);
            ScanRegistryKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\RunOnce",
                StartupAppLocation.RegistryHKLMRunOnce, apps);

            // 2. Startup Folders
            var userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            var commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);

            ScanDirectory(userStartup, StartupAppLocation.UserStartupFolder, apps);
            ScanDirectory(commonStartup, StartupAppLocation.CommonStartupFolder, apps);

            // Parallel fetch expensive info (Icons, File version info)
            Parallel.ForEach(apps, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, app =>
            {
                var appInfo = GetAppInfo(app.Command);
                var publisher = !string.IsNullOrWhiteSpace(appInfo.Publisher) ? appInfo.Publisher : appInfo.Description;
                if (string.IsNullOrWhiteSpace(publisher)) publisher = app.Location is StartupAppLocation.UserStartupFolder or StartupAppLocation.CommonStartupFolder ? "Folder Shortcut" : "Registry";

                app.Publisher = publisher;
                app.FilePath = appInfo.FilePath;
                app.LogoImage = ExtractIcon(app.Command);
            });
        });

        return apps.OrderBy(a => a.Name).ToList();
    }

    private void ScanRegistryKey(RegistryKey rootKey, string subKeyPath, StartupAppLocation location,
        List<StartupApp> apps)
    {
        try
        {
            using var key = rootKey.OpenSubKey(subKeyPath);
            if (key == null) return;

            // Determine the StartupApproved path based on Run vs RunOnce
            var approvedSubKeyPath = subKeyPath.Contains("RunOnce", StringComparison.OrdinalIgnoreCase)
                ? @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\RunOnce"
                : @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

            using var approvedKey = rootKey.OpenSubKey(approvedSubKeyPath);

            foreach (var valueName in key.GetValueNames())
            {
                var command = key.GetValue(valueName)?.ToString() ?? string.Empty;
                var isEnabled = IsStartupApproved(approvedKey, valueName);

                apps.Add(new StartupApp
                {
                    Name = valueName,
                    Command = command,
                    Location = location,
                    PathOrKey = $@"{rootKey.Name}\{subKeyPath}",
                    OriginalValueNameOrFileName = valueName,
                    IsEnabled = isEnabled
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to scan registry startup: {Path}", subKeyPath);
        }
    }

    /// <summary>
    ///     Checks the StartupApproved registry to determine if an item is enabled.
    ///     The binary data format: bytes[0] == 02 or 06 means enabled; 03 or 07 means disabled.
    ///     If no entry exists in StartupApproved, assume enabled (item is present in Run key).
    /// </summary>
    private static bool IsStartupApproved(RegistryKey? approvedKey, string valueName)
    {
        if (approvedKey == null) return true;

        try
        {
            if (approvedKey.GetValue(valueName) is byte[] { Length: >= 4 } data)
                // Disabled flags: 03, 07; Enabled flags: 02, 06
                return data[0] != 0x03 && data[0] != 0x07;
        }
        catch
        {
            // Ignore read errors, fall back to enabled
        }

        return true;
    }

    private void ScanDirectory(string dirPath, StartupAppLocation location, List<StartupApp> apps)
    {
        if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath)) return;

        try
        {
            // Determine registry root key based on folder location
            var rootKey = location == StartupAppLocation.CommonStartupFolder
                ? Registry.LocalMachine
                : Registry.CurrentUser;

            var approvedSubKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
            using var approvedKey = rootKey.OpenSubKey(approvedSubKeyPath);

            foreach (var file in Directory.GetFiles(dirPath))
            {
                var fileName = Path.GetFileName(file);

                // Hide pure .ini files like desktop.ini
                if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;

                var isEnabled = IsStartupApproved(approvedKey, fileName);
                var name = Path.GetFileNameWithoutExtension(fileName);

                apps.Add(new StartupApp
                {
                    Name = string.IsNullOrWhiteSpace(name) ? fileName : name,
                    Command = file,
                    Location = location,
                    PathOrKey = dirPath,
                    OriginalValueNameOrFileName = fileName,
                    IsEnabled = isEnabled
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to scan directory: {Dir}", dirPath);
        }
    }

    public async Task ToggleStartupApp(StartupApp app, bool enable)
    {
        await Task.Run(() =>
        {
            try
            {
                if (app.Location is StartupAppLocation.RegistryHKCURun or StartupAppLocation.RegistryHKLMRun
                    or StartupAppLocation.RegistryHKCURunOnce or StartupAppLocation.RegistryHKLMRunOnce)
                    ToggleRegistryStartupApp(app, enable);
                else // Folders
                    ToggleFolderStartupApp(app, enable);

                logger.LogInformation("Toggled startup app {Name} to {Enable}", app.Name, enable);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to toggle startup app {Name}", app.Name);
            }
        });
    }

    private static void ToggleRegistryStartupApp(StartupApp app, bool enable)
    {
        // Parse RootKey and SubKey from app.PathOrKey
        var firstSlash = app.PathOrKey.IndexOf('\\');
        if (firstSlash < 0) return;

        var rootKeyStr = app.PathOrKey[..firstSlash];
        var rootKey = rootKeyStr switch
        {
            "HKEY_CURRENT_USER" => Registry.CurrentUser,
            "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            _ => null
        };
        if (rootKey == null) return;

        // Write enable/disable to StartupApproved\Run
        var isRunOnce =
            app.Location is StartupAppLocation.RegistryHKCURunOnce or StartupAppLocation.RegistryHKLMRunOnce;
        var approvedSubKeyPath = isRunOnce
            ? @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\RunOnce"
            : @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

        using var approvedKey = rootKey.OpenSubKey(approvedSubKeyPath, true)
                                ?? rootKey.CreateSubKey(approvedSubKeyPath, true);

        // Binary format: 12 bytes. First 4 bytes = status flag, rest = timestamp (zeros for manual toggle)
        var data = new byte[12];
        data[0] = enable ? (byte)0x02 : (byte)0x03;
        approvedKey.SetValue(app.OriginalValueNameOrFileName, data, RegistryValueKind.Binary);
    }

    private static void ToggleFolderStartupApp(StartupApp app, bool enable)
    {
        var rootKey = app.Location == StartupAppLocation.CommonStartupFolder
            ? Registry.LocalMachine
            : Registry.CurrentUser;

        const string approvedSubKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
        using var approvedKey = rootKey.OpenSubKey(approvedSubKeyPath, true)
                                ?? rootKey.CreateSubKey(approvedSubKeyPath, true);

        var data = new byte[12];
        data[0] = enable ? (byte)0x02 : (byte)0x03;
        approvedKey.SetValue(app.OriginalValueNameOrFileName, data, RegistryValueKind.Binary);
    }

    public async Task<List<StartupTask>> GetStartupTasksAsync()
    {
        var tasks = new List<StartupTask>();
        try
        {
            var result = await ShellService.PowerShellAsync(@"
                Get-ScheduledTask | 
                Where-Object { $_.Triggers.CimClass.CimClassName -match 'LogonTrigger' -and $_.TaskPath -notmatch '\\Microsoft\\' } | 
                Select-Object TaskName, TaskPath, State, Description | 
                ConvertTo-Json -Depth 2
            ");

            if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Stdout))
                try
                {
                    using var doc = JsonDocument.Parse(result.Stdout);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        foreach (var el in doc.RootElement.EnumerateArray())
                            ParseTaskElement(el, tasks);
                    else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        ParseTaskElement(doc.RootElement, tasks);
                }
                catch (Exception parserEx)
                {
                    logger.LogError(parserEx, "Failed to parse json: {Json}", result.Stdout);
                }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get scheduled tasks");
        }

        return tasks.OrderBy(t => t.TaskName).ToList();
    }

    private static void ParseTaskElement(JsonElement el, List<StartupTask> tasks)
    {
        var name = el.TryGetProperty("TaskName", out var nProp) ? nProp.GetString() ?? "" : "";
        var path = el.TryGetProperty("TaskPath", out var pProp) ? pProp.GetString() ?? "" : "";
        var state = el.TryGetProperty("State", out var sProp) ? sProp.GetInt32() : 0;
        var desc = el.TryGetProperty("Description", out var dProp) ? dProp.GetString() : "";

        if (string.IsNullOrWhiteSpace(name)) return;

        tasks.Add(new StartupTask
        {
            TaskName = name,
            TaskPath = path,
            Description = desc,
            IsEnabled = state != 1 // 1 is Disabled in TaskState enum
        });
    }

    public async Task ToggleStartupTask(StartupTask task, bool enable)
    {
        try
        {
            var command = enable ? "Enable-ScheduledTask" : "Disable-ScheduledTask";
            var script = $"""
                          {command} -TaskPath "{task.TaskPath}" -TaskName "{task.TaskName}"
                          """;

            var result = await ShellService.PowerShellAsync(script);
            if (result.ExitCode != 0)
                logger.LogWarning("Toggle tasks returned exit code {ExitCode}. Stderr: {Stderr}", result.ExitCode,
                    result.Stderr);

            logger.LogInformation("Toggled scheduled task {Name} to {Enable}", task.TaskName, enable);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to toggle task {Name}", task.TaskName);
        }
    }

    private static BitmapSource? ExtractIcon(string command)
    {
        try
        {
            var path = command.Trim('\"');
            var exeIdx = path.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIdx > 0) path = path[..(exeIdx + 4)].Trim('\"', ' ', '\'');

            if (!File.Exists(path))
            {
                // try to see if it is in PATH
                if (!path.Contains('\\') && !path.Contains('/'))
                {
                    path = GetFullPathFromEnvironment(path);
                    if (string.IsNullOrEmpty(path) || !File.Exists(path))
                        return null;
                }
                else
                {
                    return null;
                }
            }

            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon != null)
            {
                var imageSource = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                imageSource.Freeze(); // Crucial for cross-thread access
                return imageSource;
            }
        }
        catch
        {
            // Ignored, fallback to generic or null
        }

        return null;
    }

    private static string? GetFullPathFromEnvironment(string fileName)
    {
        if (File.Exists(fileName))
            return Path.GetFullPath(fileName);

        var values = Environment.GetEnvironmentVariable("PATH");
        if (values == null)
            return null;

        foreach (var path in values.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(path, fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

    private (string? FilePath, string? Publisher, string? Description) GetAppInfo(string command)
    {
        try
        {
            var path = command.Trim('\"');
            var exeIdx = path.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIdx > 0) path = path[..(exeIdx + 4)].Trim('\"', ' ', '\'');

            if (!File.Exists(path))
                if (!path.Contains('\\') && !path.Contains('/'))
                    path = GetFullPathFromEnvironment(path);

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var fvi = FileVersionInfo.GetVersionInfo(path);
                return (path, fvi.CompanyName, fvi.FileDescription);
            }
        }
        catch
        {
            // Ignored, fallback
        }

        return (null, null, null);
    }
}