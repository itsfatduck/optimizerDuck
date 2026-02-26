using System.IO;
using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Models.Cleanup;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.OptimizationServices;
using Wpf.Ui.Controls;

namespace optimizerDuck.Services;

public class DiskCleanupService(ILogger<DiskCleanupService> logger)
{
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

    public async Task ScanAllAsync(IEnumerable<CleanupItem> items)
    {
        await Task.WhenAll(items.Select(ScanAsync));
    }

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
            // For Thumbnails, only count thumbcache_* files
            var searchPattern = itemId == "Thumbnails" ? "thumbcache_*" : "*";

            foreach (var file in Directory.EnumerateFiles(path, searchPattern, SearchOption.TopDirectoryOnly))
                try
                {
                    size += new FileInfo(file).Length;
                    count++;
                }
                catch
                {
                    // skip inaccessible files
                }

            // For non-thumbnail items, also include subdirectories
            if (itemId != "Thumbnails")
                foreach (var dir in Directory.EnumerateDirectories(path))
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                            try
                            {
                                size += new FileInfo(file).Length;
                                count++;
                            }
                            catch
                            {
                                // skip inaccessible files
                            }
                    }
                    catch
                    {
                        // skip inaccessible directories
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
        var searchPattern = itemId == "Thumbnails" ? "thumbcache_*" : "*";

        // Delete files
        foreach (var file in Directory.EnumerateFiles(path, searchPattern, SearchOption.TopDirectoryOnly))
            try
            {
                var length = new FileInfo(file).Length;
                File.Delete(file);
                freed += length;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not delete file: {File}", file);
            }

        // For non-thumbnail items, also delete subdirectory contents
        if (itemId != "Thumbnails")
            foreach (var dir in Directory.EnumerateDirectories(path))
                try
                {
                    foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                        try
                        {
                            var length = new FileInfo(file).Length;
                            File.Delete(file);
                            freed += length;
                        }
                        catch
                        {
                            // skip locked files
                        }

                    // Try to remove empty directories
                    try
                    {
                        Directory.Delete(dir, true);
                    }
                    catch
                    {
                        // directory may not be empty if some files were locked
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Could not access directory: {Dir}", dir);
                }

        return freed;
    }
}