using System.IO;
using Microsoft.Extensions.Logging;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.OptimizationServices;
using Wpf.Ui.Controls;
using CleanupItem = optimizerDuck.Core.Models.Optimization.Cleanup.CleanupItem;

namespace optimizerDuck.Services;

/// <summary>
///     Provides services for cleaning up disk space (temp files, caches, etc.).
/// </summary>
public class DiskCleanupService(ILogger<DiskCleanupService> logger)
{
    /// <summary>
    ///     Gets the available cleanup items.
    /// </summary>
    /// <returns>A list of cleanup items.</returns>
    public static List<CleanupItem> GetCleanupItems()
    {
        var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return
        [
            new CleanupItem
            {
                Id = "TempFiles",
                Name = Translations.DiskCleanup_Item_TempFiles,
                Description = Translations.DiskCleanup_Item_TempFiles_Description,
                Path = Path.GetTempPath(),
                Icon = SymbolRegular.Document24
            },
            new CleanupItem
            {
                Id = "SystemTemp",
                Name = Translations.DiskCleanup_Item_SystemTemp,
                Description = Translations.DiskCleanup_Item_SystemTemp_Description,
                Path = Path.Combine(windowsDir, "Temp"),
                Icon = SymbolRegular.DocumentError24
            },
            new CleanupItem
            {
                Id = "WindowsUpdate",
                Name = Translations.DiskCleanup_Item_WindowsUpdate,
                Description = Translations.DiskCleanup_Item_WindowsUpdate_Description,
                Path = Path.Combine(windowsDir, @"SoftwareDistribution\Download"),
                Icon = SymbolRegular.ArrowDownload24
            },
            new CleanupItem
            {
                Id = "Prefetch",
                Name = Translations.DiskCleanup_Item_Prefetch,
                Description = Translations.DiskCleanup_Item_Prefetch_Description,
                Path = Path.Combine(windowsDir, "Prefetch"),
                Icon = SymbolRegular.Flash24
            },
            new CleanupItem
            {
                Id = "Thumbnails",
                Name = Translations.DiskCleanup_Item_Thumbnails,
                Description = Translations.DiskCleanup_Item_Thumbnails_Description,
                Path = Path.Combine(localAppData, @"Microsoft\Windows\Explorer"),
                Icon = SymbolRegular.Image24
            },
            new CleanupItem
            {
                Id = "RecycleBin",
                Name = Translations.DiskCleanup_Item_RecycleBin,
                Description = Translations.DiskCleanup_Item_RecycleBin_Description,
                Path = "Clear-RecycleBin -Force -ErrorAction SilentlyContinue",
                Icon = SymbolRegular.Delete24,
                IsCommand = true
            },
            new CleanupItem
            {
                Id = "ErrorReports",
                Name = Translations.DiskCleanup_Item_ErrorReports,
                Description = Translations.DiskCleanup_Item_ErrorReports_Description,
                Path = Path.Combine(localAppData, "CrashDumps"),
                Icon = SymbolRegular.Bug24
            }
        ];
    }

    /// <summary>
    ///     Scans a cleanup item to calculate its size and file count.
    /// </summary>
    /// <param name="item">The cleanup item to scan.</param>
    public async Task ScanAsync(CleanupItem item)
    {
        item.IsScanning = true;
        try
        {
            if (item.IsCommand)
            {
                // For RecycleBin, estimate size via PowerShell
                if (item.Id == "RecycleBin")
                {
                    var script = "$items = (New-Object -ComObject Shell.Application).NameSpace(0xA).Items(); " +
                                 "if ($null -ne $items) { " +
                                 "  $measure = $items | Measure-Object -Property Size -Sum; " +
                                 "  $sum = if ($null -ne $measure.Sum) { $measure.Sum } else { 0 }; " +
                                 "  $count = if ($null -ne $items.Count) { $items.Count } else { 0 }; " +
                                 "  Write-Output \"$sum|$count\" " +
                                 "} else { Write-Output '0|0' }";

                    var result = await ShellService.PowerShellAsync(script);
                    var parts = result.Stdout.Trim().Split('|');
                    if (parts.Length == 2 &&
                        long.TryParse(parts[0], out var size) &&
                        long.TryParse(parts[1], out var count))
                    {
                        item.SizeBytes = size;
                        item.FileCount = count;
                    }
                    else
                    {
                        item.SizeBytes = 0;
                        item.FileCount = 0;
                    }
                }
            }
            else
            {
                var metrics = await Task.Run(() => CalculateDirectoryMetrics(item.Path, item.Id));
                item.SizeBytes = metrics.Size;
                item.FileCount = metrics.Count;
            }

            item.IsScanned = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to scan {ItemId}", item.Id);
            item.SizeBytes = 0;
            item.FileCount = 0;
            item.IsScanned = true;
        }
        finally
        {
            item.IsScanning = false;
        }
    }

    /// <summary>
    ///     Scans all cleanup items in parallel.
    /// </summary>
    /// <param name="items">The items to scan.</param>
    public async Task ScanAllAsync(IEnumerable<CleanupItem> items)
    {
        await Task.WhenAll(items.Select(ScanAsync));
    }

    /// <summary>
    ///     Cleans a single cleanup item.
    /// </summary>
    /// <param name="item">The item to clean.</param>
    /// <returns>The number of bytes freed.</returns>
    public async Task<long> CleanAsync(CleanupItem item)
    {
        item.IsCleaning = true;
        long freedBytes = 0;

        try
        {
            if (item.IsCommand)
            {
                var sizeBefore = item.SizeBytes;
                await ShellService.PowerShellAsync(item.Path);
                freedBytes = sizeBefore;
                logger.LogInformation("Cleaned {ItemId} via command, freed ~{Size}",
                    item.Id, CleanupItem.FormatBytes(freedBytes));
            }
            else
            {
                freedBytes = await Task.Run(() => DeleteFilesInDirectory(item.Path, item.Id));
                item.SizeBytes = Math.Max(0, item.SizeBytes - freedBytes);
                logger.LogInformation("Cleaned {ItemId}, freed {Size}",
                    item.Id, CleanupItem.FormatBytes(freedBytes));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clean {ItemId}", item.Id);
        }
        finally
        {
            item.IsCleaning = false;
        }

        return freedBytes;
    }

    /// <summary>
    ///     Cleans all selected items.
    /// </summary>
    /// <param name="items">The items to clean.</param>
    /// <returns>The total number of bytes freed.</returns>
    public async Task<long> CleanSelectedAsync(IEnumerable<CleanupItem> items)
    {
        long totalFreed = 0;
        foreach (var item in items.Where(i => i is { IsSelected: true, SizeBytes: > 0 }))
            totalFreed += await CleanAsync(item);
        return totalFreed;
    }

    private (long Size, long Count) CalculateDirectoryMetrics(string path, string itemId)
    {
        if (!Directory.Exists(path))
            return (0, 0);

        long size = 0;
        long count = 0;

        try
        {
            var searchPattern = itemId == "Thumbnails" ? "thumbcache_*" : "*";
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = itemId != "Thumbnails",
                ReturnSpecialDirectories = false
            };

            var dirInfo = new DirectoryInfo(path);
            var dotNetTempPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), ".net"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            foreach (var fileInfo in dirInfo.EnumerateFiles(searchPattern, options))
                try
                {
                    // Skip .net temp files if we are doing recursive search
                    if (options.RecurseSubdirectories)
                    {
                        var fullDirPath = fileInfo.DirectoryName ?? string.Empty;
                        fullDirPath = fullDirPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

                        if (fullDirPath.StartsWith(dotNetTempPath, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    // .Length is already cached from EnumerateFiles under the hood (WIN32_FIND_DATA)
                    size += fileInfo.Length;
                    count++;
                }
                catch
                {
                    // Ignore inaccessible single files
                }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error calculating metrics for {ItemId} at {Path}", itemId, path);
        }

        return (size, count);
    }

    private long DeleteFilesInDirectory(string path, string itemId)
    {
        if (!Directory.Exists(path))
            return 0;

        long freed = 0;
        var dotNetTempPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), ".net"))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var searchPattern = itemId == "Thumbnails" ? "thumbcache_*" : "*";
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = itemId != "Thumbnails",
            ReturnSpecialDirectories = false
        };

        var dirInfo = new DirectoryInfo(path);

        // Delete files
        foreach (var fileInfo in dirInfo.EnumerateFiles(searchPattern, options))
            try
            {
                if (options.RecurseSubdirectories)
                {
                    var fullDirPath = fileInfo.DirectoryName ?? string.Empty;
                    fullDirPath = fullDirPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

                    if (fullDirPath.StartsWith(dotNetTempPath, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                var length = fileInfo.Length;
                fileInfo.Delete();
                freed += length;
            }
            catch
            {
                // skip locked/inaccessible files
            }

        // Clean up empty directories if recursive
        if (options.RecurseSubdirectories)
        {
            var dirOptions = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                ReturnSpecialDirectories = false
            };

            // Order by descending length to process deepest children first
            var dirs = dirInfo.EnumerateDirectories("*", dirOptions)
                .OrderByDescending(d => d.FullName.Length)
                .ToList();

            foreach (var dir in dirs)
                try
                {
                    var fullDir = dir.FullName.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if (fullDir.StartsWith(dotNetTempPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    dir.Delete(false); // Only delete if empty
                }
                catch
                {
                    // directory may not be empty if files were locked, or access denied
                }
        }

        return freed;
    }
}