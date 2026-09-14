using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Domain.Optimizations.Models.Cleanup;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.Services.UI;
using Wpf.Ui.Controls;

namespace optimizerDuck.Test.Services.UI;

public class DiskCleanupServiceTests
{
    private static DiskCleanupService NewService() => new(NullLogger<DiskCleanupService>.Instance);

    [Fact]
    public async Task CleanAsync_DoesNotDeleteFilesInDotNetDirectory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"optimizerDuck_Test_{Guid.NewGuid():N}");
        var dotNetDir = Path.Combine(tempRoot, ".net", "test-package");
        var normalDir = Path.Combine(tempRoot, "normal-temp");
        var cancellationToken = TestContext.Current.CancellationToken;

        Directory.CreateDirectory(dotNetDir);
        Directory.CreateDirectory(normalDir);

        var normalFile = Path.Combine(normalDir, "temp.txt");
        var dotNetFile = Path.Combine(dotNetDir, "scratch.txt");

        await File.WriteAllTextAsync(normalFile, "normal temp file", cancellationToken);
        await File.WriteAllTextAsync(dotNetFile, "active dotnet scratch file", cancellationToken);

        try
        {
            var service = NewService();
            var item = new CleanupItem
            {
                Id = "TempFiles",
                NameKey = "Temp Files",
                DescriptionKey = "Temp Description",
                Path = tempRoot,
                Icon = SymbolRegular.Document24,
            };

            var freed = await service.CleanAsync(item);

            Assert.False(File.Exists(normalFile));
            // DotNet scratch file MUST NOT be deleted
            if (
                dotNetDir.StartsWith(
                    Path.Combine(Path.GetTempPath(), ".net"),
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                Assert.True(File.Exists(dotNetFile));
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                try
                {
                    Directory.Delete(tempRoot, true);
                }
                catch
                {
                    // Ignore cleanup error
                }
            }
        }
    }

    [Fact]
    public async Task ScanAsync_CalculatesCorrectMetrics()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            $"optimizerDuck_ScanTest_{Guid.NewGuid():N}"
        );
        var cancellationToken = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(tempRoot);
        var file1 = Path.Combine(tempRoot, "file1.txt");
        var file2 = Path.Combine(tempRoot, "file2.txt");

        await File.WriteAllTextAsync(file1, "12345", cancellationToken);
        await File.WriteAllTextAsync(file2, "12345", cancellationToken);

        try
        {
            var service = NewService();
            var item = new CleanupItem
            {
                Id = "TempFiles",
                NameKey = "Temp Files",
                DescriptionKey = "Temp Description",
                Path = tempRoot,
                Icon = SymbolRegular.Document24,
            };

            await service.ScanAsync(item);

            Assert.True(item.IsScanned);
            Assert.Equal(2, item.FileCount);
            Assert.Equal(10, item.SizeBytes);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                try
                {
                    Directory.Delete(tempRoot, true);
                }
                catch
                {
                    // Ignore cleanup error
                }
            }
        }
    }

    [Fact]
    public void GetCleanupItems_RecycleBinItem_HasNoCommandPath()
    {
        var item = DiskCleanupService.GetCleanupItems().Single(i => i.Id == "RecycleBin");

        // Still flagged as "not a directory", but the PowerShell command is gone: size and
        // emptying now come from the shell Recycle Bin APIs.
        Assert.True(item.IsCommand);
        Assert.Empty(item.Path);
        Assert.False(item.CanOpenFolder);
    }

    [Fact]
    public async Task CleanAsync_RecycleBinItem_ReportsNoFreedBytesForAnEmptyBin()
    {
        // The failing-empty half of the contract lives in RecycleBinServiceTests
        // (Empty_InvalidRoot_FailsWithError): DiskCleanupService always empties every drive, so a
        // failure cannot be forced here without either destroying real data or adding a
        // test-only seam to the service.
        var totals = RecycleBinService.Query();
        if (totals.ItemCount != 0)
        {
            Assert.Skip(
                $"Recycle Bin holds {totals.ItemCount} item(s); emptying it here would destroy real user data, so this test only runs on an empty bin."
            );
        }

        var service = NewService();
        var item = DiskCleanupService.GetCleanupItems().Single(i => i.Id == "RecycleBin");

        await service.ScanAsync(item);
        Assert.Equal(0, item.SizeBytes);

        var freed = await service.CleanAsync(item);

        Assert.Equal(0, freed);
    }
}
