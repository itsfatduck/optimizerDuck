using System.Collections.ObjectModel;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using optimizerDuck.Domain.Customize.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Services.Optimization.Providers;
using Xunit;

namespace optimizerDuck.Test.Domain.Customize;

public class BaseCustomizeSettingTests : IDisposable
{
    private const string TestKeyPath = @"HKCU\Software\TestOptimizerDuckCustomize";

    private class TestCustomizeSetting : BaseCustomizeSetting
    {
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

    public BaseCustomizeSettingTests()
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
            hkcu.DeleteSubKeyTree(@"Software\TestOptimizerDuckCustomize", false);
        }
        catch
        {
            // Ignore if it doesn't exist
        }
    }

    [Fact]
    public async Task ApplyAsync_True_WritesCorrectValueToRegistry()
    {
        var setting = new TestCustomizeSetting { OwnerType = typeof(TestCustomizeSetting) };

        await setting.ApplyAsync(true);

        var value = RegistryService.Read<int>(new RegistryItem(TestKeyPath, "TestValue"));
        Assert.Equal(1, value);
    }

    [Fact]
    public async Task ApplyAsync_False_WritesCorrectValueToRegistry()
    {
        var setting = new TestCustomizeSetting { OwnerType = typeof(TestCustomizeSetting) };

        await setting.ApplyAsync(false);

        var value = RegistryService.Read<int>(new RegistryItem(TestKeyPath, "TestValue"));
        Assert.Equal(0, value);
    }

    [Fact]
    public async Task GetStateAsync_ReturnsCorrectState()
    {
        var setting = new TestCustomizeSetting { OwnerType = typeof(TestCustomizeSetting) };

        // Initially should be false (value doesn't exist)
        var initialState = await setting.GetStateAsync();
        Assert.False(initialState);

        // Enable
        await setting.ApplyAsync(true);
        var enabledState = await setting.GetStateAsync();
        Assert.True(enabledState);

        // Disable
        await setting.ApplyAsync(false);
        var disabledState = await setting.GetStateAsync();
        Assert.False(disabledState);
    }

    [Fact]
    public async Task ApplyAsync_DoesNotBlockCallingThread()
    {
        var setting = new TestCustomizeSetting { OwnerType = typeof(TestCustomizeSetting) };

        // Measure time - should be fast since it's offloaded to thread pool
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await setting.ApplyAsync(true);
        await setting.ApplyAsync(false);

        stopwatch.Stop();

        // Should complete quickly (not blocking on UI thread equivalent)
        Assert.True(
            stopwatch.ElapsedMilliseconds < 1000,
            $"Operations took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms"
        );
    }

    [Fact]
    public async Task MultipleApplies_ExecutesSequentiallyWithoutCorruption()
    {
        var setting = new TestCustomizeSetting { OwnerType = typeof(TestCustomizeSetting) };

        // Perform multiple rapid toggles
        for (int i = 0; i < 10; i++)
        {
            await setting.ApplyAsync(true);
            var enabledValue = RegistryService.Read<int>(
                new RegistryItem(TestKeyPath, "TestValue")
            );
            Assert.Equal(1, enabledValue);

            await setting.ApplyAsync(false);
            var disabledValue = RegistryService.Read<int>(
                new RegistryItem(TestKeyPath, "TestValue")
            );
            Assert.Equal(0, disabledValue);
        }

        // Final state should be disabled
        var finalState = await setting.GetStateAsync();
        Assert.False(finalState);
    }
}
