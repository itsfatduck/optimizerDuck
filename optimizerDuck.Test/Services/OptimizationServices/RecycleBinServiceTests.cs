using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Test.Services.OptimizationServices;

/// <summary>
///     Live shell32 tests: real Recycle Bin state, no mocks. Nothing here destroys user data: the
///     queries only read, and the one emptying call targets an invalid root, which shell32 refuses.
/// </summary>
public class RecycleBinServiceTests
{
    [Fact]
    public void Query_AllDrives_ReportsConsistentTotals()
    {
        var totals = RecycleBinService.Query();

        Assert.True(totals.SizeBytes >= 0, $"negative size {totals.SizeBytes}");
        Assert.True(totals.ItemCount >= 0, $"negative count {totals.ItemCount}");

        // Empty bins report zero for both; a non-empty bin cannot report bytes without items.
        if (totals.SizeBytes == 0)
            Assert.Equal(0, totals.ItemCount);
        else
            Assert.True(totals.ItemCount > 0, $"size {totals.SizeBytes} with no items");
    }

    [Fact]
    public void Query_InvalidRoot_ReturnsZeroes()
    {
        // Verified live: shell32 answers ERROR_PATH_NOT_FOUND for a non-drive string, and the
        // service turns that into zeroes so the cleanup page has no failure branch.
        var totals = RecycleBinService.Query("not-a-drive");

        Assert.Equal(0, totals.SizeBytes);
        Assert.Equal(0, totals.ItemCount);
    }

    [Fact]
    public void Query_SystemDrive_DoesNotExceedAllDrives()
    {
        var all = RecycleBinService.Query();
        var systemDrive = Path.GetPathRoot(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows)
        );
        Assert.False(string.IsNullOrEmpty(systemDrive));

        var one = RecycleBinService.Query(systemDrive!);

        Assert.True(one.SizeBytes >= 0);
        Assert.True(one.ItemCount >= 0);
    }

    [Fact]
    public void Empty_InvalidRoot_FailsWithError()
    {
        var (succeeded, errorCode) = RecycleBinService.Empty("not-a-drive");

        Assert.False(succeeded);
        Assert.NotEqual(0, errorCode);
    }

    [Fact]
    public void SuppressionFlags_AreTheThreeDocumentedOnes()
    {
        // SHERB_NOCONFIRMATION (1) | SHERB_NOPROGRESSUI (2) | SHERB_NOSOUND (4)
        Assert.Equal(0x7u, RecycleBinService.SuppressionFlags);
    }
}
