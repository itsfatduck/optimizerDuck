using System.Collections.ObjectModel;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using optimizerDuck.Domain.Features.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Services.OptimizationServices;
using Xunit;

namespace optimizerDuck.Test.Domain.Features;

public class BaseFeatureTests : IDisposable
{
    private const string TestKeyPath = @"HKCU\Software\TestOptimizerDuckFeatures";

    private class TestFeature : BaseFeature
    {
        // FeatureKey is not virtual, so we don't override it
        // The test will use the actual class name

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = TestKeyPath,
                Name = "TestValue",
                OnValue = 1,
                OffValue = 0,
                ValueKind = RegistryValueKind.DWord,
            },
        ];
    }

    public BaseFeatureTests()
    {
        CleanupTestKeys();
    }

    public void Dispose()
    {
        CleanupTestKeys();
    }

    private static void CleanupTestKeys()
    {
        try
        {
            using var hkcu = Registry.CurrentUser;
            hkcu.DeleteSubKeyTree(@"Software\TestOptimizerDuckFeatures", false);
        }
        catch
        {
            // Ignore if it doesn't exist
        }
    }

    [Fact]
    public async Task EnableAsync_WritesCorrectValueToRegistry()
    {
        var feature = new TestFeature { OwnerType = typeof(TestFeature) };

        await feature.EnableAsync();

        var value = RegistryService.Read<int>(new RegistryItem(TestKeyPath, "TestValue"));
        Assert.Equal(1, value);
    }

    [Fact]
    public async Task DisableAsync_WritesCorrectValueToRegistry()
    {
        var feature = new TestFeature { OwnerType = typeof(TestFeature) };

        await feature.DisableAsync();

        var value = RegistryService.Read<int>(new RegistryItem(TestKeyPath, "TestValue"));
        Assert.Equal(0, value);
    }

    [Fact]
    public async Task GetStateAsync_ReturnsCorrectState()
    {
        var feature = new TestFeature { OwnerType = typeof(TestFeature) };

        // Initially should be false (value doesn't exist)
        var initialState = await feature.GetStateAsync();
        Assert.False(initialState);

        // Enable
        await feature.EnableAsync();
        var enabledState = await feature.GetStateAsync();
        Assert.True(enabledState);

        // Disable
        await feature.DisableAsync();
        var disabledState = await feature.GetStateAsync();
        Assert.False(disabledState);
    }

    [Fact]
    public async Task ToggleOperations_DoesNotBlockCallingThread()
    {
        var feature = new TestFeature { OwnerType = typeof(TestFeature) };

        // Measure time - should be fast since it's offloaded to thread pool
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await feature.EnableAsync();
        await feature.DisableAsync();

        stopwatch.Stop();

        // Should complete quickly (not blocking on UI thread equivalent)
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"Operations took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms");
    }

    [Fact]
    public async Task MultipleToggles_ExecutesSequentiallyWithoutCorruption()
    {
        var feature = new TestFeature { OwnerType = typeof(TestFeature) };

        // Perform multiple rapid toggles
        for (int i = 0; i < 10; i++)
        {
            await feature.EnableAsync();
            var enabledValue = RegistryService.Read<int>(new RegistryItem(TestKeyPath, "TestValue"));
            Assert.Equal(1, enabledValue);

            await feature.DisableAsync();
            var disabledValue = RegistryService.Read<int>(new RegistryItem(TestKeyPath, "TestValue"));
            Assert.Equal(0, disabledValue);
        }

        // Final state should be disabled
        var finalState = await feature.GetStateAsync();
        Assert.False(finalState);
    }
}
