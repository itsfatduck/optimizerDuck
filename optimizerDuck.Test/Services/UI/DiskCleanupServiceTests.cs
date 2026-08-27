using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Domain.Optimizations.Models.Cleanup;
using optimizerDuck.Services.UI;
using Wpf.Ui.Controls;

namespace optimizerDuck.Test.Services.UI;

public class DiskCleanupServiceTests
{
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
            var service = new DiskCleanupService(NullLogger<DiskCleanupService>.Instance);
            var item = new CleanupItem
            {
                Id = "TempFiles",
                Name = "Temp Files",
                Description = "Temp Description",
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
            var service = new DiskCleanupService(NullLogger<DiskCleanupService>.Instance);
            var item = new CleanupItem
            {
                Id = "TempFiles",
                Name = "Temp Files",
                Description = "Temp Description",
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
}
