using System.Collections.ObjectModel;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using optimizerDuck.Domain.Abstractions;
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

    #region GetStateWithRetryAsync helpers

    /// <summary>Always returns the same stable state.</summary>
    private sealed class StableStateSetting : BaseCustomizeSetting
    {
        public bool State { get; set; }

        public override Task<bool> GetStateAsync() => Task.Run(() => State);
    }

    /// <summary>Uses base GetStateAsync (no toggles → returns false).</summary>
    private sealed class EmptyTogglesSetting : BaseCustomizeSetting
    {
        // No RegistryToggles override → empty
        // No GetStateAsync override → uses base (empty toggles → false)
    }

    /// <summary>Alternates on every call — never stabilises.</summary>
    private sealed class OscillatingStateSetting : BaseCustomizeSetting
    {
        private int _callCount;

        public override Task<bool> GetStateAsync() =>
            Task.Run(() => Interlocked.Increment(ref _callCount) % 2 == 1);
    }

    /// <summary>Returns stable after a configurable delay.</summary>
    private sealed class DelayedStateSetting : BaseCustomizeSetting
    {
        public bool State { get; set; } = true;
        public int CallCount { get; set; }
        public int DelayMs { get; set; }

        public override async Task<bool> GetStateAsync()
        {
            if (DelayMs > 0)
                await Task.Delay(DelayMs);
            CallCount++;
            return State;
        }
    }

    /// <summary>Setting with multiple toggles to test path dedup.</summary>
    private sealed class MultiToggleSetting : BaseCustomizeSetting
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new() { Path = @"HKCU\Software\TestA", Name = "Val1" },
                new() { Path = @"HKCU\Software\TestB", Name = "Val2" },
                new() { Path = @"HKCU\Software\TestA", Name = "Val3" },
            ];
    }

    #endregion

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

    #region GetStateWithRetryAsync tests

    [Fact]
    public async Task GetStateWithRetryAsync_StableState_ReturnsTrue()
    {
        var setting = new StableStateSetting { State = true };

        var result = await setting.GetStateWithRetryAsync(maxRetries: 3, delayMs: 10);

        Assert.True(result);
    }

    [Fact]
    public async Task GetStateWithRetryAsync_StableState_ReturnsFalse()
    {
        var setting = new StableStateSetting { State = false };

        var result = await setting.GetStateWithRetryAsync(maxRetries: 3, delayMs: 10);

        Assert.False(result);
    }

    [Fact]
    public async Task GetStateWithRetryAsync_StableState_ConvergesQuickly()
    {
        var setting = new StableStateSetting { State = true };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await setting.GetStateWithRetryAsync(maxRetries: 10, delayMs: 200);
        sw.Stop();

        // Should converge on 2nd read (< 200ms) not wait for all 10 retries
        Assert.True(result);
        Assert.True(
            sw.ElapsedMilliseconds < 300,
            $"Took {sw.ElapsedMilliseconds}ms, expected < 300ms (convergence should be fast)"
        );
    }

    [Fact]
    public async Task GetStateWithRetryAsync_OscillatingState_UsesAllRetriesAndReturnsLast()
    {
        var setting = new OscillatingStateSetting();

        var result = await setting.GetStateWithRetryAsync(maxRetries: 5, delayMs: 10);

        // 5 reads with alternating values: T, F, T, F, T → never converges → returns last (true)
        Assert.True(result);
    }

    [Fact]
    public async Task GetStateWithRetryAsync_WithNoToggles_ReturnsFalse()
    {
        // Empty toggles → base GetStateAsync returns false
        var setting = new EmptyTogglesSetting();

        var result = await setting.GetStateWithRetryAsync(maxRetries: 2, delayMs: 10);

        Assert.False(result);
    }

    [Fact]
    public async Task GetStateWithRetryAsync_ThroughInterface_WorksCorrectly()
    {
        ICustomizeSetting setting = new StableStateSetting { State = true };

        var result = await setting.GetStateWithRetryAsync(maxRetries: 2, delayMs: 10);

        Assert.True(result);
    }

    [Fact]
    public async Task GetStateWithRetryAsync_AfterApply_ReturnsCorrectState()
    {
        var setting = new TestCustomizeSetting { OwnerType = typeof(TestCustomizeSetting) };

        await setting.ApplyAsync(true);
        var result = await setting.GetStateWithRetryAsync(maxRetries: 3, delayMs: 10);

        Assert.True(result);

        await setting.ApplyAsync(false);
        result = await setting.GetStateWithRetryAsync(maxRetries: 3, delayMs: 10);

        Assert.False(result);
    }

    #endregion

    #region WatchedRegistryPaths tests

    [Fact]
    public void WatchedRegistryPaths_WhenMultipleToggles_ReturnsDistinctPaths()
    {
        ICustomizeSetting setting = new MultiToggleSetting();

        var paths = setting.WatchedRegistryPaths;

        // 3 toggles but only 2 distinct paths
        Assert.Equal(2, paths.Count);
        Assert.Contains(@"HKCU\Software\TestA", paths);
        Assert.Contains(@"HKCU\Software\TestB", paths);
    }

    [Fact]
    public void WatchedRegistryPaths_WhenSingleToggle_ReturnsOnePath()
    {
        ICustomizeSetting setting = new TestCustomizeSetting();

        var paths = setting.WatchedRegistryPaths;

        Assert.Single(paths);
        Assert.Equal(TestKeyPath, paths[0]);
    }

    [Fact]
    public void WatchedRegistryPaths_WhenNoToggles_ReturnsEmpty()
    {
        // StableStateSetting has no toggles overrides → empty
        ICustomizeSetting setting = new StableStateSetting();

        var paths = setting.WatchedRegistryPaths;

        Assert.Empty(paths);
    }

    #endregion
}
