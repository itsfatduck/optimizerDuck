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

    #region RefreshScope tests

    private static readonly System.Reflection.PropertyInfo RefreshScopeProperty =
        typeof(BaseCustomizeSetting).GetProperty(
            "RefreshScope",
            System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public
        )!;

    private static readonly System.Reflection.PropertyInfo NeedsPostActionProperty =
        typeof(BaseCustomizeSetting).GetProperty(
            "NeedsPostAction",
            System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public
        )!;

    private static CustomizeRefreshScope GetRefreshScope(BaseCustomizeSetting s) =>
        (CustomizeRefreshScope)RefreshScopeProperty.GetValue(s)!;

    private static bool GetNeedsPostAction(BaseCustomizeSetting s) =>
        (bool)NeedsPostActionProperty.GetValue(s)!;

    /// <summary>Plain setting with no overrides - must be opt-in for post-action.</summary>
    private sealed class DefaultScopeSetting : BaseCustomizeSetting
    {
        public override Task<bool> GetStateAsync() => Task.FromResult(false);
    }

    /// <summary>Setting that opts into the default explorer-level refresh.</summary>
    private sealed class DefaultExplorerScopeSetting : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Default;

        public override Task<bool> GetStateAsync() => Task.FromResult(false);
    }

    /// <summary>Setting that opts into the desktop-icon refresh.</summary>
    private sealed class DesktopIconsScopeSetting : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope =>
            CustomizeRefreshScope.DesktopIcons;

        public override Task<bool> GetStateAsync() => Task.FromResult(false);
    }

    /// <summary>Setting that opts into the global HideIcons cache refresh.</summary>
    private sealed class HideDesktopIconsScopeSetting : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope =>
            CustomizeRefreshScope.HideDesktopIcons;

        public override Task<bool> GetStateAsync() => Task.FromResult(false);
    }

    /// <summary>Setting that opts into the taskbar refresh.</summary>
    private sealed class TaskbarScopeSetting : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope =>
            CustomizeRefreshScope.TaskbarSettings;

        public override Task<bool> GetStateAsync() => Task.FromResult(false);
    }

    /// <summary>Setting that opts into a multi-flag custom scope.</summary>
    private sealed class MultiScopeSetting : BaseCustomizeSetting
    {
        protected override CustomizeRefreshScope RefreshScope =>
            CustomizeRefreshScope.Settings
            | CustomizeRefreshScope.Associations
            | CustomizeRefreshScope.PolicyUpdate;

        public override Task<bool> GetStateAsync() => Task.FromResult(false);
    }

    [Fact]
    public void RefreshScope_ByDefault_IsNone_SoPostActionDoesNotRun()
    {
        var setting = new DefaultScopeSetting();

        Assert.Equal(CustomizeRefreshScope.None, GetRefreshScope(setting));
        Assert.False(GetNeedsPostAction(setting));
    }

    [Fact]
    public void NeedsPostAction_IsDerivedFromRefreshScope()
    {
        Assert.True(GetNeedsPostAction(new DefaultExplorerScopeSetting()));
        Assert.True(GetNeedsPostAction(new DesktopIconsScopeSetting()));
        Assert.True(GetNeedsPostAction(new HideDesktopIconsScopeSetting()));
        Assert.True(GetNeedsPostAction(new TaskbarScopeSetting()));
        Assert.True(GetNeedsPostAction(new MultiScopeSetting()));
        Assert.False(GetNeedsPostAction(new DefaultScopeSetting()));
    }

    [Fact]
    public void RefreshScope_OnEachSubclass_MatchesExpectedValue()
    {
        Assert.Equal(CustomizeRefreshScope.Default, GetRefreshScope(new DefaultExplorerScopeSetting()));
        Assert.Equal(CustomizeRefreshScope.DesktopIcons, GetRefreshScope(new DesktopIconsScopeSetting()));
        Assert.Equal(CustomizeRefreshScope.TaskbarSettings, GetRefreshScope(new TaskbarScopeSetting()));
        Assert.Equal(
            CustomizeRefreshScope.Settings
                | CustomizeRefreshScope.Associations
                | CustomizeRefreshScope.PolicyUpdate,
            GetRefreshScope(new MultiScopeSetting())
        );
    }

    [Fact]
    public async Task ApplyAsync_WhenRefreshScopeIsNone_DoesNotCallPostAction()
    {
        var setting = new DefaultScopeSetting { OwnerType = typeof(DefaultScopeSetting) };

        // With RefreshScope = None, the base ApplyAsync must skip
        // ExecutePostActionAsync entirely. We confirm the gate behaviour:
        // NeedsPostAction is false, so no refresh runs.
        Assert.False(GetNeedsPostAction(setting));
        await setting.ApplyAsync(false); // must not throw
    }

    [Fact]
    public async Task ApplyAsync_WhenRefreshScopeIsSet_RunsWithoutThrowing()
    {
        // Smoke test: every declared scope variant must produce a working
        // ApplyAsync that touches the registry and runs the refresh pipeline
        // without exceptions on a real Windows host.
        var settings = new BaseCustomizeSetting[]
        {
            new DefaultExplorerScopeSetting { OwnerType = typeof(DefaultExplorerScopeSetting) },
            new DesktopIconsScopeSetting { OwnerType = typeof(DesktopIconsScopeSetting) },
            new TaskbarScopeSetting { OwnerType = typeof(TaskbarScopeSetting) },
            new MultiScopeSetting { OwnerType = typeof(MultiScopeSetting) },
        };

        foreach (var setting in settings)
        {
            await setting.ApplyAsync(true);
            await setting.ApplyAsync(false);
        }
    }

    [Fact]
    public void CustomizeRefreshScope_DefaultEqualsSettingsPlusAssociations()
    {
        Assert.Equal(
            CustomizeRefreshScope.Settings | CustomizeRefreshScope.Associations,
            CustomizeRefreshScope.Default
        );
    }

    [Fact]
    public void CustomizeRefreshScope_DesktopIconsEqualsSettingsPlusDesktop()
    {
        Assert.Equal(
            CustomizeRefreshScope.Settings | CustomizeRefreshScope.Desktop,
            CustomizeRefreshScope.DesktopIcons
        );
    }

    [Fact]
    public void CustomizeRefreshScope_TaskbarSettingsEqualsSettingsPlusTaskbar()
    {
        Assert.Equal(
            CustomizeRefreshScope.Settings | CustomizeRefreshScope.Taskbar,
            CustomizeRefreshScope.TaskbarSettings
        );
    }

    [Fact]
    public void CustomizeRefreshScope_ExplorerViewIncludesPolicyUpdate()
    {
        Assert.True(CustomizeRefreshScope.ExplorerView.HasFlag(CustomizeRefreshScope.PolicyUpdate));
    }

    [Fact]
    public void CustomizeRefreshScope_AllFlagsCanBeCombined()
    {
        var all = CustomizeRefreshScope.Settings
            | CustomizeRefreshScope.Associations
            | CustomizeRefreshScope.Desktop
            | CustomizeRefreshScope.Taskbar
            | CustomizeRefreshScope.PolicyUpdate
            | CustomizeRefreshScope.Theme;

        Assert.True(all.HasFlag(CustomizeRefreshScope.Settings));
        Assert.True(all.HasFlag(CustomizeRefreshScope.Theme));
    }

    #endregion
}
