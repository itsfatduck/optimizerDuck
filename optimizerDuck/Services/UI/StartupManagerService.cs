using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using optimizerDuck.Domain.Optimizations.Models.StartupManager;
using optimizerDuck.Services.Optimization.Providers;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using StartupApp = optimizerDuck.Domain.Optimizations.Models.StartupManager.StartupApp;
using StartupTask = optimizerDuck.Domain.Optimizations.Models.StartupManager.StartupTask;

namespace optimizerDuck.Services.UI;

public class StartupManagerService(ILogger<StartupManagerService> logger)
{
    /// <summary>
    ///     Retrieves all startup applications from registry Run/RunOnce keys (including the 32-bit
    ///     Wow6432Node view), startup folders, and packaged (UWP / MSIX) apps with StartupTask
    ///     declarations, including their enabled state and icons.
    /// </summary>
    /// <returns>A list of <see cref="StartupApp"/> instances sorted by name.</returns>
    public async Task<List<StartupApp>> GetStartupAppsAsync()
    {
        var apps = new List<StartupApp>();

        await Task.Run(() =>
        {
            // 1. Registry (default view)
            ScanRegistryKey(
                Registry.CurrentUser,
                @"Software\Microsoft\Windows\CurrentVersion\Run",
                StartupAppLocation.RegistryHKCURun,
                apps
            );
            ScanRegistryKey(
                Registry.LocalMachine,
                @"Software\Microsoft\Windows\CurrentVersion\Run",
                StartupAppLocation.RegistryHKLMRun,
                apps
            );
            ScanRegistryKey(
                Registry.CurrentUser,
                @"Software\Microsoft\Windows\CurrentVersion\RunOnce",
                StartupAppLocation.RegistryHKCURunOnce,
                apps
            );
            ScanRegistryKey(
                Registry.LocalMachine,
                @"Software\Microsoft\Windows\CurrentVersion\RunOnce",
                StartupAppLocation.RegistryHKLMRunOnce,
                apps
            );

            // 2. Registry (32-bit view, redirected to Wow6432Node) — where 32-bit installers register
            using var hklm32 = RegistryKey.OpenBaseKey(
                RegistryHive.LocalMachine,
                RegistryView.Registry32
            );
            ScanRegistryKey(
                hklm32,
                @"Software\Microsoft\Windows\CurrentVersion\Run",
                StartupAppLocation.RegistryHKLMRun32,
                apps
            );
            ScanRegistryKey(
                hklm32,
                @"Software\Microsoft\Windows\CurrentVersion\RunOnce",
                StartupAppLocation.RegistryHKLMRunOnce32,
                apps
            );

            // 3. Startup Folders
            var userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            var commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);

            ScanDirectory(userStartup, StartupAppLocation.UserStartupFolder, apps);
            ScanDirectory(commonStartup, StartupAppLocation.CommonStartupFolder, apps);

            // 4. Packaged (UWP / MSIX) apps declaring a StartupTask
            var uwpEntries = ScanUwpStartupTasks();
            apps.AddRange(uwpEntries.Select(e => e.App));

            // Parallel fetch expensive info (Icons, File version info)
            Parallel.ForEach(
                apps,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                app =>
                {
                    var appInfo = GetAppInfo(app.Command);
                    var publisher = !string.IsNullOrWhiteSpace(appInfo.Publisher)
                        ? appInfo.Publisher
                        : appInfo.Description;
                    if (string.IsNullOrWhiteSpace(publisher))
                        publisher = app.Location switch
                        {
                            StartupAppLocation.UserStartupFolder
                            or StartupAppLocation.CommonStartupFolder => "Folder Shortcut",
                            StartupAppLocation.UwpStartupTask => app.Publisher, // preset from package identity
                            _ => "Registry",
                        };

                    app.Publisher = publisher;
                    app.FilePath ??= appInfo.FilePath;
                    app.LogoImage ??= ExtractIcon(app.Command);
                }
            );

            // Fill remaining packaged-app icons from the package logo PNG (apps without a win32 exe)
            Parallel.ForEach(
                uwpEntries.Where(e => e.App.LogoImage == null && e.LogoPath != null),
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                e => e.App.LogoImage = LoadFrozenBitmapImage(e.LogoPath!)
            );
        });

        logger.LogInformation("Retrieved {Count} startup apps", apps.Count);

        return apps.OrderBy(a => a.Name).ToList();
    }

    private void ScanRegistryKey(
        RegistryKey rootKey,
        string subKeyPath,
        StartupAppLocation location,
        List<StartupApp> apps
    )
    {
        try
        {
            using var key = rootKey.OpenSubKey(subKeyPath);
            if (key == null)
                return;

            // 32-bit Run entries keep their approved flags in the dedicated Run32/RunOnce32
            // subkeys (default view), and Windows 11 treats them as disabled until an explicit
            // enable flag exists — unlike 64-bit entries, where a missing flag means enabled.
            var is32Bit =
                location
                is StartupAppLocation.RegistryHKLMRun32
                    or StartupAppLocation.RegistryHKLMRunOnce32;
            var approvedSubKeyPath = GetApprovedSubKeyPath(location);

            using var approvedKey = (is32Bit ? Registry.LocalMachine : rootKey).OpenSubKey(
                approvedSubKeyPath
            );

            foreach (var valueName in key.GetValueNames())
            {
                var command = key.GetValue(valueName)?.ToString() ?? string.Empty;
                var isEnabled = IsStartupApproved(approvedKey, valueName, !is32Bit);

                apps.Add(
                    new StartupApp
                    {
                        Name = valueName,
                        Command = command,
                        Location = location,
                        PathOrKey = $@"{rootKey.Name}\{subKeyPath}",
                        OriginalValueNameOrFileName = valueName,
                        IsEnabled = isEnabled,
                    }
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to scan registry startup: {Path}", subKeyPath);
        }
    }

    private static string GetApprovedSubKeyPath(StartupAppLocation location) =>
        location switch
        {
            StartupAppLocation.RegistryHKLMRun32 =>
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32",
            StartupAppLocation.RegistryHKLMRunOnce32 =>
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\RunOnce32",
            StartupAppLocation.RegistryHKCURunOnce or StartupAppLocation.RegistryHKLMRunOnce =>
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\RunOnce",
            _ => @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
        };

    /// <summary>
    ///     Checks the StartupApproved registry to determine if an item is enabled.
    ///     The binary data format: bytes[0] == 02 or 06 means enabled; 03 or 07 means disabled.
    ///     If no entry exists in StartupApproved, fall back to <paramref name="defaultEnabled"/>
    ///     (64-bit/folder entries default to enabled; 32-bit Run entries default to disabled).
    /// </summary>
    private static bool IsStartupApproved(
        RegistryKey? approvedKey,
        string valueName,
        bool defaultEnabled
    )
    {
        if (approvedKey == null)
            return defaultEnabled;

        try
        {
            if (approvedKey.GetValue(valueName) is byte[] { Length: >= 4 } data)
                // Disabled flags: 03, 07; Enabled flags: 02, 06
                return data[0] != 0x03 && data[0] != 0x07;
        }
        catch
        {
            // Ignore read errors, fall back to the default state
        }

        return defaultEnabled;
    }

    private void ScanDirectory(string dirPath, StartupAppLocation location, List<StartupApp> apps)
    {
        if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath))
            return;

        try
        {
            // Determine registry root key based on folder location
            var rootKey =
                location == StartupAppLocation.CommonStartupFolder
                    ? Registry.LocalMachine
                    : Registry.CurrentUser;

            const string approvedSubKeyPath =
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
            using var approvedKey = rootKey.OpenSubKey(approvedSubKeyPath);

            foreach (var file in Directory.GetFiles(dirPath))
            {
                var fileName = Path.GetFileName(file);

                // Hide pure .ini files like desktop.ini
                if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                    continue;

                var isEnabled = IsStartupApproved(approvedKey, fileName, defaultEnabled: true);
                var name = Path.GetFileNameWithoutExtension(fileName);

                apps.Add(
                    new StartupApp
                    {
                        Name = string.IsNullOrWhiteSpace(name) ? fileName : name,
                        Command = file,
                        Location = location,
                        PathOrKey = dirPath,
                        OriginalValueNameOrFileName = fileName,
                        IsEnabled = isEnabled,
                    }
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to scan directory: {Dir}", dirPath);
        }
    }

    private sealed record UwpStartupEntry(StartupApp App, string? LogoPath);

    private const string SystemAppDataRoot =
        @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData";

    /// <summary>
    ///     Enumerates packaged (UWP / MSIX) apps that declare a StartupTask in their manifest.
    ///     The enable state comes from <c>HKCU\...\AppModel\SystemAppData\{FamilyName}\{TaskId}\State</c>
    ///     (0=Disabled, 1=DisabledByUser, 2=Enabled, 4=EnabledByPolicy); when no state exists yet,
    ///     the manifest's Enabled attribute decides.
    /// </summary>
    private List<UwpStartupEntry> ScanUwpStartupTasks()
    {
        var entries = new List<UwpStartupEntry>();

        try
        {
            var sid = WindowsIdentity.GetCurrent().User?.Value;
            if (sid == null)
                return entries;

            var packages = new PackageManager().FindPackagesForUser(sid);

            foreach (var package in packages)
            {
                try
                {
                    var manifestPath = Path.Combine(
                        package.InstalledLocation.Path,
                        "AppxManifest.xml"
                    );
                    if (!File.Exists(manifestPath))
                        continue;

                    var manifest = XDocument.Load(manifestPath);
                    var startupTasks = manifest
                        .Descendants()
                        .Where(e => e.Name.LocalName == "StartupTask")
                        .ToList();
                    if (startupTasks.Count == 0)
                        continue;

                    var displayName = ResolvePackageName(package);
                    var publisher = package.Id.Publisher;
                    if (publisher.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                        publisher = publisher[3..];
                    var exePath = ResolvePackageExecutable(
                        manifest,
                        package.InstalledLocation.Path
                    );
                    var logoPath = ResolvePackageLogoPath(package);

                    foreach (var task in startupTasks)
                    {
                        var taskId = (string?)task.Attribute("TaskId");
                        if (string.IsNullOrWhiteSpace(taskId))
                            continue;

                        var manifestEnabled = string.Equals(
                            (string?)task.Attribute("Enabled"),
                            "true",
                            StringComparison.OrdinalIgnoreCase
                        );

                        using var stateKey = Registry.CurrentUser.OpenSubKey(
                            $"{SystemAppDataRoot}\\{package.Id.FamilyName}\\{taskId}"
                        );
                        var state = stateKey?.GetValue("State") as int?;
                        var isEnabled = state.HasValue ? state is 2 or 4 : manifestEnabled;

                        entries.Add(
                            new UwpStartupEntry(
                                new StartupApp
                                {
                                    Name =
                                        startupTasks.Count > 1
                                            ? $"{displayName} ({taskId})"
                                            : displayName,
                                    Command = exePath ?? package.Id.FamilyName,
                                    Location = StartupAppLocation.UwpStartupTask,
                                    PathOrKey =
                                        $@"{Registry.CurrentUser.Name}\{SystemAppDataRoot}\{package.Id.FamilyName}",
                                    OriginalValueNameOrFileName = taskId,
                                    IsEnabled = isEnabled,
                                    Publisher = publisher,
                                    FilePath =
                                        exePath != null && File.Exists(exePath) ? exePath : null,
                                },
                                logoPath
                            )
                        );
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to scan startup task of package {Package}",
                        package.Id.FullName
                    );
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to scan packaged app startup tasks");
        }

        return entries;
    }

    private static string ResolvePackageName(Package package)
    {
        var name = package.DisplayName?.Trim();
        if (
            !string.IsNullOrWhiteSpace(name)
            && !name.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase)
        )
            return name;

        return package.Id.Name;
    }

    private static string? ResolvePackageExecutable(XDocument manifest, string installPath)
    {
        var exe = manifest
            .Descendants()
            .Where(e => e.Name.LocalName == "Application")
            .Select(e => (string?)e.Attribute("Executable"))
            .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));
        if (exe == null)
            return null;

        return Path.IsPathRooted(exe) ? exe : Path.Combine(installPath, exe);
    }

    private static string? ResolvePackageLogoPath(Package package)
    {
        try
        {
            var logo = package.Logo;
            if (logo == null)
                return null;

            string path;
            if (logo.IsAbsoluteUri && logo.IsFile)
                path = logo.LocalPath;
            else if (
                logo.IsAbsoluteUri
                && logo.Scheme.Equals("ms-appx", StringComparison.OrdinalIgnoreCase)
            )
                path = Path.Combine(
                    package.InstalledLocation.Path,
                    logo.AbsolutePath.TrimStart('/')
                );
            else
                return null;

            if (File.Exists(path))
                return path;

            // The logo may be declared without a scale qualifier; probe the usual variants
            var dir = Path.GetDirectoryName(path);
            if (dir == null)
                return null;
            var baseName = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            foreach (
                var suffix in new[]
                {
                    ".scale-200",
                    ".scale-150",
                    ".scale-125",
                    ".scale-100",
                    ".targetsize-48",
                    ".targetsize-36",
                }
            )
            {
                var candidate = Path.Combine(dir, baseName + suffix + ext);
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        catch
        {
            // ignored, no logo for this package
        }

        return null;
    }

    private static BitmapImage? LoadFrozenBitmapImage(string path)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            // ignored, fall back to no icon
        }

        return null;
    }

    /// <summary>Enables or disables a startup application by writing the StartupApproved registry flag.</summary>
    /// <param name="app">The startup app to toggle.</param>
    /// <param name="enable"><see langword="true"/> to enable, <see langword="false"/> to disable.</param>
    public async Task ToggleStartupApp(StartupApp app, bool enable)
    {
        await Task.Run(() =>
        {
            try
            {
                if (
                    app.Location
                    is StartupAppLocation.RegistryHKCURun
                        or StartupAppLocation.RegistryHKLMRun
                        or StartupAppLocation.RegistryHKCURunOnce
                        or StartupAppLocation.RegistryHKLMRunOnce
                        or StartupAppLocation.RegistryHKLMRun32
                        or StartupAppLocation.RegistryHKLMRunOnce32
                )
                    ToggleRegistryStartupApp(app, enable);
                else if (app.Location == StartupAppLocation.UwpStartupTask)
                    ToggleUwpStartupApp(app, enable);
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
        if (firstSlash < 0)
            return;

        var rootKeyStr = app.PathOrKey[..firstSlash];
        var hive = rootKeyStr switch
        {
            "HKEY_CURRENT_USER" => (RegistryHive?)RegistryHive.CurrentUser,
            "HKEY_LOCAL_MACHINE" => RegistryHive.LocalMachine,
            _ => null,
        };
        if (hive == null)
            return;

        // Write the flag; 32-bit entries use the dedicated Run32/RunOnce32 subkeys
        var approvedSubKeyPath = GetApprovedSubKeyPath(app.Location);

        // Binary format: 12 bytes. First 4 bytes = status flag, rest = timestamp (zeros for manual toggle)
        var data = new byte[12];
        data[0] = enable ? (byte)0x02 : (byte)0x03;

        using var rootKey = RegistryKey.OpenBaseKey(hive.Value, RegistryView.Default);
        WriteApprovedFlag(rootKey, approvedSubKeyPath, app.OriginalValueNameOrFileName, data);
    }

    private static void WriteApprovedFlag(
        RegistryKey rootKey,
        string subKeyPath,
        string valueName,
        byte[] data
    )
    {
        using var approvedKey =
            rootKey.OpenSubKey(subKeyPath, true) ?? rootKey.CreateSubKey(subKeyPath, true);
        approvedKey.SetValue(valueName, data, RegistryValueKind.Binary);
    }

    private static void ToggleUwpStartupApp(StartupApp app, bool enable)
    {
        // Packaged-app startup state: 2 = Enabled, 1 = DisabledByUser
        var firstSlash = app.PathOrKey.IndexOf('\\');
        if (firstSlash < 0)
            return;

        var subKeyPath = app.PathOrKey[(firstSlash + 1)..];
        using var taskKey = Registry.CurrentUser.CreateSubKey(
            $@"{subKeyPath}\{app.OriginalValueNameOrFileName}",
            true
        );
        taskKey.SetValue("State", enable ? 2 : 1, RegistryValueKind.DWord);
    }

    private static void ToggleFolderStartupApp(StartupApp app, bool enable)
    {
        var rootKey =
            app.Location == StartupAppLocation.CommonStartupFolder
                ? Registry.LocalMachine
                : Registry.CurrentUser;

        const string approvedSubKeyPath =
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
        using var approvedKey =
            rootKey.OpenSubKey(approvedSubKeyPath, true)
            ?? rootKey.CreateSubKey(approvedSubKeyPath, true);

        var data = new byte[12];
        data[0] = enable ? (byte)0x02 : (byte)0x03;
        approvedKey.SetValue(app.OriginalValueNameOrFileName, data, RegistryValueKind.Binary);
    }

    /// <summary>Retrieves all startup scheduled tasks from the Windows Task Scheduler, including their enabled state and icons.</summary>
    /// <returns>A list of <see cref="StartupTask"/> instances sorted by name.</returns>
    public Task<List<StartupTask>> GetStartupTasksAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                var models = ScheduledTaskService.GetStartupTasks();
                var tasks = models
                    .Select(m => new StartupTask
                    {
                        TaskName = m.Name,
                        TaskPath = m.Path,
                        Description = m.Description,
                        TriggerSummary = m.TriggerSummary,
                        TriggerTypes = [.. m.TriggerTypes],
                        ActionSummary = m.ActionSummary,
                        IsEnabled = m.IsEnabled,
                    })
                    .OrderBy(t => t.TaskName)
                    .ToList();

                // Extract icons from task commands
                Parallel.ForEach(
                    tasks,
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    task =>
                    {
                        if (!string.IsNullOrWhiteSpace(task.ActionSummary))
                            task.LogoImage = ExtractIcon(task.ActionSummary);
                    }
                );

                return tasks;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get scheduled tasks");
                return [];
            }
        });
    }

    /// <summary>Enables or disables a startup scheduled task using the Task Scheduler API.</summary>
    /// <param name="task">The startup task to toggle.</param>
    /// <param name="enable"><see langword="true"/> to enable, <see langword="false"/> to disable.</param>
    public Task ToggleStartupTask(StartupTask task, bool enable)
    {
        return Task.Run(() =>
        {
            try
            {
                var fullPath = task.TaskPath.TrimEnd('\\') + "\\" + task.TaskName;
                if (enable)
                {
                    ScheduledTaskService.EnableTask(fullPath);
                    logger.LogInformation("Enabled task {Name} ({Path})", task.TaskName, fullPath);
                }
                else
                {
                    ScheduledTaskService.DisableTask(fullPath);
                    logger.LogInformation("Disabled task {Name} ({Path})", task.TaskName, fullPath);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to toggle task {Name}", task.TaskName);
            }
        });
    }

    /// <summary>Extracts the associated icon from an executable path or command string. Expands environment variables and searches PATH if needed.</summary>
    /// <param name="command">The command or file path to extract the icon from.</param>
    /// <returns>A frozen <see cref="BitmapSource"/> suitable for cross-thread UI binding, or <see langword="null"/> if the icon cannot be extracted.</returns>
    /// <remarks>
    ///     Uses <c>SHGetFileInfo</c> with <c>SHGFI_LARGEICON</c> (48x48) for higher quality icons
    ///     where possible, falling back to <see cref="Icon.ExtractAssociatedIcon"/> (32x32) if the
    ///     P/Invoke approach fails.
    /// </remarks>
    public static BitmapSource? ExtractIcon(string command)
    {
        try
        {
            var path = ResolveExecutablePath(command);
            if (path == null)
                return null;

            var iconSource = ExtractIconWithShGetFileInfo(path);
            if (iconSource != null)
                return iconSource;

            // fallback to ExtractAssociatedIcon (32x32)
            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon != null)
            {
                var imageSource = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions()
                );
                imageSource.Freeze();
                return imageSource;
            }
        }
        catch
        {
            // ignored, fallback to generic or null
        }

        return null;
    }

    private static string? ResolveExecutablePath(string command)
    {
        var path = command.Trim('\"');

        // expand environment variables (e.g., %USERPROFILE%, %ProgramFiles%)
        path = Environment.ExpandEnvironmentVariables(path);
        var exeIdx = path.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIdx > 0)
            path = path[..(exeIdx + 4)].Trim('\"', ' ', '\'');

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

        return path;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags
    );

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000; // 48x48

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private static BitmapSource? ExtractIconWithShGetFileInfo(string path)
    {
        var shfi = new SHFILEINFO();
        var result = SHGetFileInfo(
            path,
            0,
            ref shfi,
            (uint)Marshal.SizeOf<SHFILEINFO>(),
            SHGFI_ICON | SHGFI_LARGEICON
        );

        if (result != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
        {
            try
            {
                var imageSource = Imaging.CreateBitmapSourceFromHIcon(
                    shfi.hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions()
                );
                imageSource.Freeze();
                return imageSource;
            }
            finally
            {
                NativeMethods.DestroyIcon(shfi.hIcon);
            }
        }

        return null;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool DestroyIcon(IntPtr hIcon);
    }

    /// <summary>Resolves a file name to its full path by searching the directories listed in the PATH environment variable.</summary>
    /// <param name="fileName">The file name (e.g., "notepad.exe") to resolve.</param>
    /// <returns>The full path if found, otherwise <see langword="null"/>.</returns>
    public static string? GetFullPathFromEnvironment(string fileName)
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
            if (exeIdx > 0)
                path = path[..(exeIdx + 4)].Trim('\"', ' ', '\'');

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
