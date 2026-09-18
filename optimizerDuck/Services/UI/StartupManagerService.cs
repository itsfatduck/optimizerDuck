using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.StartupManager;
using optimizerDuck.Services.System.Primitives;
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

            // 2. Registry (32-bit view, redirected to Wow6432Node), where 32-bit installers
            // register
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

            // Fill remaining packaged-app icons from the package logo PNG (apps without a
            // win32 exe)
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
            // enable flag exists, unlike 64-bit entries, where a missing flag means enabled.
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
    ///     The per-user repository of installed packages. Its subkey names are package full names and
    ///     <c>PackageRootFolder</c> is the install location, which is what the manifest scan needs.
    ///     Reading it here keeps the publish free of the Windows SDK projection that
    ///     <c>PackageManager</c> drags in.
    /// </summary>
    private const string PackageRepositoryRoot =
        @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

    /// <summary>
    ///     Enumerates packaged (UWP / MSIX) apps that declare a StartupTask in their manifest. The
    ///     enable state comes from
    ///     <c>HKCU\...\AppModel\SystemAppData\{FamilyName}\{TaskId}\State</c>
    ///     (0=Disabled, 1=DisabledByUser, 2=Enabled, 4=EnabledByPolicy); when no state exists yet,
    ///     the manifest's Enabled attribute decides.
    /// </summary>
    private List<UwpStartupEntry> ScanUwpStartupTasks()
    {
        var entries = new List<UwpStartupEntry>();

        try
        {
            using var repository = Registry.CurrentUser.OpenSubKey(PackageRepositoryRoot);
            if (repository == null)
                return entries;

            foreach (var packageKeyName in repository.GetSubKeyNames())
            {
                using var packageKey = repository.OpenSubKey(packageKeyName);
                if (packageKey?.GetValue("PackageRootFolder") is not string installPath)
                    continue;

                var manifestPath = Path.Combine(installPath, "AppxManifest.xml");
                if (!File.Exists(manifestPath))
                    continue;

                try
                {
                    // Read the manifest as text first: the packages that declare no startup task are
                    // the vast majority, and a substring check beats an XML document for each.
                    var manifestText = File.ReadAllText(manifestPath);
                    if (!manifestText.Contains("StartupTask", StringComparison.Ordinal))
                        continue;

                    var manifest = XDocument.Parse(manifestText);
                    var startupTasks = manifest
                        .Descendants()
                        .Where(e => e.Name.LocalName == "StartupTask")
                        .ToList();
                    if (startupTasks.Count == 0)
                        continue;

                    var familyName = PackageFamilyName(
                        packageKey.GetValue("PackageID") as string ?? packageKeyName
                    );
                    if (familyName == null)
                        continue;

                    var identity = manifest
                        .Descendants()
                        .FirstOrDefault(e => e.Name.LocalName == "Identity");
                    var displayName = ResolvePackageName(
                        manifest,
                        identity,
                        packageKey.GetValue("DisplayName") as string
                    );
                    var publisher = ResolvePublisher(identity);
                    var logoPath = ResolvePackageLogoPath(manifest, installPath);

                    foreach (var task in startupTasks)
                    {
                        var taskId = (string?)task.Attribute("TaskId");
                        if (string.IsNullOrWhiteSpace(taskId))
                            continue;

                        // The application that declares the task is the one the shell names, and its
                        // executable is the one this entry opens.
                        var application = task.Ancestors()
                            .FirstOrDefault(e => e.Name.LocalName == "Application");
                        var name =
                            ResolveAppName(familyName, (string?)application?.Attribute("Id"))
                            ?? displayName;

                        var manifestEnabled = string.Equals(
                            (string?)task.Attribute("Enabled"),
                            "true",
                            StringComparison.OrdinalIgnoreCase
                        );

                        using var stateKey = Registry.CurrentUser.OpenSubKey(
                            $"{SystemAppDataRoot}\\{familyName}\\{taskId}"
                        );
                        var state = stateKey?.GetValue("State") as int?;
                        var isEnabled = state.HasValue ? state is 2 or 4 : manifestEnabled;

                        var exePath = ResolvePackageExecutable(manifest, application, installPath);

                        entries.Add(
                            new UwpStartupEntry(
                                new StartupApp
                                {
                                    Name = startupTasks.Count > 1 ? $"{name} ({taskId})" : name,
                                    Command = exePath ?? familyName,
                                    Location = StartupAppLocation.UwpStartupTask,
                                    PathOrKey =
                                        $@"{Registry.CurrentUser.Name}\{SystemAppDataRoot}\{familyName}",
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
                        packageKeyName
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

    /// <summary>
    ///     Reads the package family name out of a package full name
    ///     (<c>Name_Version_Architecture_ResourceId_PublisherId</c>). A package name cannot carry an
    ///     underscore, so the first and last segment are the ones a family name is made of.
    /// </summary>
    /// <param name="packageFullName">The package full name.</param>
    /// <returns>The family name, or <see langword="null" /> when the name is not parseable.</returns>
    internal static string? PackageFamilyName(string packageFullName)
    {
        var segments = packageFullName.Split('_');
        if (segments.Length < 2)
            return null;

        var name = segments[0];
        var publisherId = segments[^1];
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(publisherId))
            return null;

        return $"{name}_{publisherId}";
    }

    /// <summary>Resolves the name a package declares, falling back to its identity name.</summary>
    private static string ResolvePackageName(
        XDocument manifest,
        XElement? identity,
        string? repositoryDisplayName
    )
    {
        var properties = manifest
            .Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Properties");
        var declaredName = (string?)
            properties?.Elements().FirstOrDefault(e => e.Name.LocalName == "DisplayName");

        var name = ReadDisplayName(declaredName) ?? ReadDisplayName(repositoryDisplayName);
        if (name != null)
            return name;

        return (string?)identity?.Attribute("Name") ?? string.Empty;
    }

    /// <summary>
    ///     Turns a manifest or repository display name into text, or <see langword="null" /> when it
    ///     names a resource this process cannot resolve.
    /// </summary>
    internal static string? ReadDisplayName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var resolved = value.StartsWith('@') ? IndirectString.Resolve(value) : value;
        resolved = resolved?.Trim();

        return
            string.IsNullOrWhiteSpace(resolved)
            || resolved.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase)
            ? null
            : resolved;
    }

    /// <summary>
    ///     Reads the name the shell shows for a packaged app, which is the localized one: the shell
    ///     resolves the resource names a manifest leaves as <c>ms-resource:</c>.
    /// </summary>
    private static string? ResolveAppName(string familyName, string? applicationId)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
            return null;

        try
        {
            var name = ShellDisplayName
                .Read($@"shell:AppsFolder\{familyName}!{applicationId}")
                ?.Trim();
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch
        {
            // The shell does not know this app, so the package name is the best name there is.
            return null;
        }
    }

    private static string ResolvePublisher(XElement? identity)
    {
        var publisher = (string?)identity?.Attribute("Publisher") ?? string.Empty;
        return publisher.StartsWith("CN=", StringComparison.OrdinalIgnoreCase)
            ? publisher[3..]
            : publisher;
    }

    private static string? ResolvePackageExecutable(
        XDocument manifest,
        XElement? application,
        string installPath
    )
    {
        var exe =
            (string?)application?.Attribute("Executable")
            ?? manifest
                .Descendants()
                .Where(e => e.Name.LocalName == "Application")
                .Select(e => (string?)e.Attribute("Executable"))
                .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));
        if (string.IsNullOrWhiteSpace(exe))
            return null;

        return Path.IsPathRooted(exe) ? exe : Path.Combine(installPath, exe);
    }

    /// <summary>Resolves the logo a manifest declares against the package's install location.</summary>
    private static string? ResolvePackageLogoPath(XDocument manifest, string installPath)
    {
        var properties = manifest
            .Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Properties");
        var declared =
            (string?)properties?.Elements().FirstOrDefault(e => e.Name.LocalName == "Logo")
            ?? manifest
                .Descendants()
                .Where(e => e.Name.LocalName is "VisualElements" or "DefaultTile")
                .Select(e =>
                    (string?)e.Attribute("Square44x44Logo") ?? (string?)e.Attribute("Logo")
                )
                .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));

        return ResolveManifestPath(declared, installPath);
    }

    private static string? ResolveManifestPath(string? declared, string installPath)
    {
        if (string.IsNullOrWhiteSpace(declared))
            return null;

        try
        {
            var path = declared;
            if (
                Uri.TryCreate(declared, UriKind.Absolute, out var uri)
                && uri.Scheme.Equals("ms-appx", StringComparison.OrdinalIgnoreCase)
            )
                path = uri.AbsolutePath.TrimStart('/');
            else if (Uri.TryCreate(declared, UriKind.Absolute, out uri) && uri.IsFile)
                path = uri.LocalPath;

            path = Path.IsPathRooted(path)
                ? path
                : Path.Combine(installPath, path.Replace('/', '\\'));

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

    /// <summary>Resolves an indirect string (<c>@...</c>) through the shell's own resolver.</summary>
    private static class IndirectString
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int SHLoadIndirectString(
            string source,
            StringBuilder buffer,
            int bufferLength,
            IntPtr reserved
        );

        internal static string? Resolve(string source)
        {
            var buffer = new StringBuilder(1024);
            return SHLoadIndirectString(source, buffer, buffer.Capacity, IntPtr.Zero) == 0
                ? buffer.ToString()
                : null;
        }
    }

    /// <summary>Reads the display name the shell gives a shell namespace item.</summary>
    private static class ShellDisplayName
    {
        private const uint NormalDisplay = 0;

        private static readonly Guid ShellItemId = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");

        internal static string? Read(string parsingName)
        {
            IShellItem? item = null;
            try
            {
                SHCreateItemFromParsingName(parsingName, IntPtr.Zero, in ShellItemId, out item);
                item.GetDisplayName(NormalDisplay, out var name);
                try
                {
                    return Marshal.PtrToStringUni(name);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(name);
                }
            }
            finally
            {
                if (item != null)
                    Marshal.ReleaseComObject(item);
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            string path,
            IntPtr bindContext,
            in Guid iid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem item
        );

        [ComImport]
        [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(
                IntPtr bindContext,
                ref Guid handler,
                ref Guid iid,
                out IntPtr result
            );

            void GetParent(out IShellItem parent);

            void GetDisplayName(uint format, out IntPtr name);

            void GetAttributes(uint mask, out uint attributes);

            void Compare(IShellItem other, uint hint, out int order);
        }
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

    /// <summary>
    ///     Enables or disables a startup application by writing the StartupApproved registry flag.
    /// </summary>
    /// <param name="app">The startup app to toggle.</param>
    /// <param name="enable">
    ///     <see langword="true"/> to enable, <see langword="false"/> to disable.
    /// </param>
    /// <returns>The toggle outcome.</returns>
    public Task<OpResult> ToggleStartupApp(StartupApp app, bool enable)
    {
        return Task.Run(() =>
        {
            try
            {
                var result =
                    app.Location
                        is StartupAppLocation.RegistryHKCURun
                            or StartupAppLocation.RegistryHKLMRun
                            or StartupAppLocation.RegistryHKCURunOnce
                            or StartupAppLocation.RegistryHKLMRunOnce
                            or StartupAppLocation.RegistryHKLMRun32
                            or StartupAppLocation.RegistryHKLMRunOnce32
                        ? ToggleRegistryStartupApp(app, enable)
                    : app.Location == StartupAppLocation.UwpStartupTask
                        ? ToggleUwpStartupApp(app, enable)
                    : ToggleFolderStartupApp(app, enable);

                if (result.Ok)
                    logger.LogInformation(
                        "Toggled startup app {Name} to {Enable}",
                        app.Name,
                        enable
                    );
                else
                    logger.LogError(
                        "Failed to toggle startup app {Name}: {Error}",
                        app.Name,
                        result.Error
                    );

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to toggle startup app {Name}", app.Name);
                return OpResult.Fail(ex.Message, ex.ToString());
            }
        });
    }

    private static OpResult ToggleRegistryStartupApp(StartupApp app, bool enable)
    {
        var firstSlash = app.PathOrKey.IndexOf('\\');
        if (firstSlash < 0)
            return OpResult.Fail(
                ServiceStrings.Format(ServiceStrings.StartupAppErrorUnsupportedLocation, app.Name)
            );

        var rootKeyStr = app.PathOrKey[..firstSlash];
        var hive = rootKeyStr switch
        {
            "HKEY_CURRENT_USER" => (RegistryHive?)RegistryHive.CurrentUser,
            "HKEY_LOCAL_MACHINE" => RegistryHive.LocalMachine,
            _ => null,
        };
        if (hive == null)
            return OpResult.Fail(
                ServiceStrings.Format(ServiceStrings.StartupAppErrorUnsupportedLocation, app.Name)
            );

        // 32-bit entries use the dedicated Run32/RunOnce32 subkeys
        var approvedSubKeyPath = GetApprovedSubKeyPath(app.Location);

        using var rootKey = RegistryKey.OpenBaseKey(hive.Value, RegistryView.Default);
        WriteApprovedFlag(
            rootKey,
            approvedSubKeyPath,
            app.OriginalValueNameOrFileName,
            BuildApprovedData(enable)
        );
        return OpResult.Success();
    }

    /// <summary>
    ///     Builds the 12-byte StartupApproved value Task Manager writes: first 4 bytes = status
    ///     flag (02 enabled / 03 disabled), trailing 8 bytes = FILETIME of the change (shown as
    ///     the disable date in Task Manager).
    /// </summary>
    private static byte[] BuildApprovedData(bool enable)
    {
        var data = new byte[12];
        data[0] = enable ? (byte)0x02 : (byte)0x03;
        BitConverter.GetBytes(DateTime.UtcNow.ToFileTime()).CopyTo(data, 4);
        return data;
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

    private static OpResult ToggleUwpStartupApp(StartupApp app, bool enable)
    {
        // Packaged-app startup state: 2 = Enabled, 1 = DisabledByUser
        var firstSlash = app.PathOrKey.IndexOf('\\');
        if (firstSlash < 0)
            return OpResult.Fail(
                ServiceStrings.Format(ServiceStrings.StartupAppErrorUnsupportedLocation, app.Name)
            );

        var subKeyPath = app.PathOrKey[(firstSlash + 1)..];
        using var taskKey = Registry.CurrentUser.CreateSubKey(
            $@"{subKeyPath}\{app.OriginalValueNameOrFileName}",
            true
        );
        if (taskKey == null)
            return OpResult.Fail(
                ServiceStrings.Format(ServiceStrings.StartupAppErrorUnsupportedLocation, app.Name)
            );

        taskKey.SetValue("State", enable ? 2 : 1, RegistryValueKind.DWord);
        return OpResult.Success();
    }

    private static OpResult ToggleFolderStartupApp(StartupApp app, bool enable)
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
        if (approvedKey == null)
            return OpResult.Fail(
                ServiceStrings.Format(ServiceStrings.StartupAppErrorUnsupportedLocation, app.Name)
            );

        approvedKey.SetValue(
            app.OriginalValueNameOrFileName,
            BuildApprovedData(enable),
            RegistryValueKind.Binary
        );
        return OpResult.Success();
    }

    /// <summary>
    ///     Retrieves all startup scheduled tasks from the Windows Task Scheduler, including their
    ///     enabled state and icons.
    /// </summary>
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
                        TriggerInfos = [.. m.TriggerInfos],
                        ActionSummary = m.ActionSummary,
                        IsEnabled = m.IsEnabled,
                    })
                    .OrderBy(t => t.TaskName)
                    .ToList();

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

    /// <summary>
    ///     Enables or disables a startup scheduled task using the Task Scheduler API.
    /// </summary>
    /// <param name="call">The run the toggle records its steps into.</param>
    /// <param name="task">The startup task to toggle.</param>
    /// <param name="enable">
    ///     <see langword="true"/> to enable, <see langword="false"/> to disable.
    /// </param>
    /// <returns>The toggle outcome.</returns>
    public Task<OpResult> ToggleStartupTask(OpCall call, StartupTask task, bool enable)
    {
        ArgumentNullException.ThrowIfNull(call);

        return Task.Run(() =>
        {
            try
            {
                var fullPath = task.TaskPath.TrimEnd('\\') + "\\" + task.TaskName;
                var result = enable
                    ? ScheduledTaskService.EnableTask(call, fullPath)
                    : ScheduledTaskService.DisableTask(call, fullPath);

                // The provider reports a missing task as nothing to change, which is right for an
                // optimization. From this list it is a stale entry the user cannot toggle, so the
                // page says so instead of reporting a success that is not there.
                if (
                    result.Ok
                    && call.Changes.Changes.Any(step => step.Kind == ChangeKind.NotApplicable)
                )
                    return OpResult.Fail(
                        ServiceStrings.Format(
                            ServiceStrings.ScheduledTaskInfoSkippedNotFound,
                            fullPath
                        )
                    );

                if (!result.Ok)
                    logger.LogError(
                        "Failed to toggle task {Name}: {Error}",
                        task.TaskName,
                        result.Error
                    );

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to toggle task {Name}", task.TaskName);
                return OpResult.Fail(ex.Message, ex.ToString());
            }
        });
    }

    /// <summary>
    ///     Extracts the associated icon from an executable path or command string. Expands
    ///     environment variables and searches PATH if needed.
    /// </summary>
    /// <param name="command">The command or file path to extract the icon from.</param>
    /// <returns>
    ///     A frozen <see cref="BitmapSource"/> suitable for cross-thread UI binding, or
    ///     <see langword="null"/> if the icon cannot be extracted.
    /// </returns>
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

        path = Environment.ExpandEnvironmentVariables(path);
        var exeIdx = path.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIdx > 0)
            path = path[..(exeIdx + 4)].Trim('\"', ' ', '\'');

        if (!File.Exists(path))
        {
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

    /// <summary>
    ///     Resolves a file name to its full path by searching the directories listed in the PATH
    ///     environment variable.
    /// </summary>
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
