using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Customize.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.Optimization.Providers;
using optimizerDuck.Services.System;
using optimizerDuck.UI.ViewModels.Customize;
using Wpf.Ui.Controls;

namespace optimizerDuck.Test.UI.ViewModels;

public class CustomizeItemViewModelTests : IDisposable
{
    private const string TestKeyPath = @"HKCU\Software\TestOptimizerDuckCustomizeVm";
    private const string TestKeyPathNative = @"Software\TestOptimizerDuckCustomizeVm";
    private const string RegName = "DropdownVmTest";

    public CustomizeItemViewModelTests()
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
            hkcu.DeleteSubKeyTree(TestKeyPathNative, false);
        }
        catch
        {
            // Ignore if it doesn't exist
        }
    }

    [CustomizeSetting(Icon = SymbolRegular.Settings24)]
    private sealed class TestDropdownVmSetting : BaseCustomizeSetting
    {
        public int ApplyCount;

        public override CustomizeControlType ControlType => CustomizeControlType.Dropdown;

        protected override IReadOnlyList<SettingOption>? GetOptions() =>
        [
            Option("OptionA", TestKeyPath, RegName, 1),
            Option("OptionB", TestKeyPath, RegName, 2),
        ];

        public override async Task ApplyAsync(object? value)
        {
            ApplyCount++;
            await base.ApplyAsync(value);
        }
    }

    private sealed class FakeRegistryWatcher : IRegistryWatcher
    {
        public event EventHandler<string>? RegistryKeyChanged;

        public List<string> WatchedPaths { get; } = [];
        public List<string> UnwatchedPaths { get; } = [];

        public void Watch(string registryPath) => WatchedPaths.Add(registryPath);

        public void Unwatch(string registryPath) => UnwatchedPaths.Add(registryPath);

        public void Raise(string path) => RegistryKeyChanged?.Invoke(this, path);

        public void Dispose() { }
    }

    private static (TestDropdownVmSetting setting, FakeRegistryWatcher watcher, CustomizeItemViewModel vm)
        CreateVm()
    {
        var setting = new TestDropdownVmSetting { OwnerType = typeof(TestDropdownVmSetting) };
        var watcher = new FakeRegistryWatcher();
        var vm = new CustomizeItemViewModel(setting, NullLoggerFactory.Instance, watcher);
        return (setting, watcher, vm);
    }

    private static void WriteValue(int value) =>
        RegistryService.Write(new RegistryItem(TestKeyPath, RegName, value));

    private static void DeleteValue() =>
        RegistryService.DeleteValue(new RegistryItem(TestKeyPath, RegName));

    private static object? ReadValue() =>
        RegistryService.Read<object>(new RegistryItem(TestKeyPath, RegName));

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var token = TestContext.Current.CancellationToken;
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condition not met within timeout");
            await Task.Delay(25, token);
        }
    }

    #region Load state

    [Fact]
    public async Task LoadState_OutOfScopeValue_ShowsCustomOptionWithRawValue()
    {
        WriteValue(99);

        var (setting, watcher, vm) = CreateVm();
        await vm.LoadStateAsync();

        Assert.Equal(99, vm.CurrentValue);
        Assert.NotNull(vm.Options);
        Assert.Equal(3, vm.Options!.Count);
        Assert.Equal(99, vm.Options[2].Value);
        Assert.Equal(
            Loc.Instance[BaseCustomizeSetting.CustomOptionTranslationKey],
            vm.Options[2].DisplayName
        );
        Assert.Contains(TestKeyPath, watcher.WatchedPaths);
    }

    [Fact]
    public async Task LoadState_MissingValue_ShowsNotSetOptionWithSentinel()
    {
        DeleteValue();

        var (_, _, vm) = CreateVm();
        await vm.LoadStateAsync();

        Assert.Same(BaseCustomizeSetting.MissingValueSentinel, vm.CurrentValue);
        Assert.Equal(3, vm.Options!.Count);
        Assert.Equal(
            Loc.Instance[BaseCustomizeSetting.CustomOptionNotSetTranslationKey],
            vm.Options[2].DisplayName
        );
        Assert.Same(BaseCustomizeSetting.MissingValueSentinel, vm.Options[2].Value);
    }

    [Fact]
    public async Task LoadState_InScopeValue_NoCustomOption()
    {
        WriteValue(2);

        var (_, _, vm) = CreateVm();
        await vm.LoadStateAsync();

        Assert.Equal(2, vm.CurrentValue);
        Assert.Equal(2, vm.Options!.Count);
        Assert.DoesNotContain(vm.Options, o => o.Value is 99);
    }

    #endregion

    #region User selection flows

    [Fact]
    public async Task SelectDeclaredOption_FromCustomState_AppliesAndRemovesCustom()
    {
        WriteValue(99);

        var (setting, _, vm) = CreateVm();
        await vm.LoadStateAsync();
        Assert.Equal(3, vm.Options!.Count); // Custom shown

        // Simulate the ComboBox selection of a declared option.
        vm.CurrentValue = 2;

        await WaitUntilAsync(() => Equals(ReadValue(), 2) && !vm.IsLoading);

        Assert.Equal(1, setting.ApplyCount);
        Assert.Equal(2, vm.CurrentValue);
        Assert.Equal(2, vm.Options!.Count); // Custom gone
        Assert.DoesNotContain(vm.Options, o => Equals(o.Value, 99));
    }

    [Fact]
    public async Task SelectDeclaredOption_FromNotSetState_AppliesAndRemovesNotSet()
    {
        DeleteValue();

        var (setting, _, vm) = CreateVm();
        await vm.LoadStateAsync();
        Assert.Equal(3, vm.Options!.Count); // Not set shown

        vm.CurrentValue = 1;

        await WaitUntilAsync(() => Equals(ReadValue(), 1) && !vm.IsLoading);

        Assert.Equal(1, setting.ApplyCount);
        Assert.Equal(1, vm.CurrentValue);
        Assert.Equal(2, vm.Options!.Count); // Not set gone
        Assert.DoesNotContain(
            vm.Options,
            o => ReferenceEquals(o.Value, BaseCustomizeSetting.MissingValueSentinel)
        );
    }

    [Fact]
    public async Task SelectCurrentCustomValue_DoesNotReapply()
    {
        WriteValue(99);

        var (setting, _, vm) = CreateVm();
        await vm.LoadStateAsync();

        // Selecting the already-selected "Custom" option is a synchronous no-op: the
        // value equals the live state, so no apply is queued (IsLoading stays false)
        // and nothing is written.
        vm.CurrentValue = 99;

        Assert.False(vm.IsLoading);
        Assert.Equal(0, setting.ApplyCount);
        Assert.Equal(99, ReadValue());
    }

    [Fact]
    public async Task SelectNotSetOption_DoesNotReapply()
    {
        DeleteValue();

        var (setting, _, vm) = CreateVm();
        await vm.LoadStateAsync();
        Assert.Equal(3, vm.Options!.Count); // Not set shown

        // Selecting the already-selected "Not set" fallback is a synchronous no-op: the
        // sentinel equals the live state, so no apply is queued (IsLoading stays false)
        // and nothing is written or deleted.
        vm.CurrentValue = BaseCustomizeSetting.MissingValueSentinel;

        Assert.False(vm.IsLoading);
        Assert.Equal(0, setting.ApplyCount);
        Assert.Null(ReadValue());
    }

    [Fact]
    public async Task SelectDeclaredOption_ThenExternalOutOfScopeChange_ShowsCustomAgain()
    {
        WriteValue(99);

        var (_, watcher, vm) = CreateVm();
        await vm.LoadStateAsync();

        // Switch to declared option 1.
        vm.CurrentValue = 1;
        await WaitUntilAsync(() => Equals(ReadValue(), 1) && !vm.IsLoading);
        Assert.Equal(2, vm.Options!.Count);

        // External change pushes the value out of scope again.
        WriteValue(123);
        watcher.Raise(TestKeyPath);

        await WaitUntilAsync(() => Equals(vm.CurrentValue, 123) && vm.Options!.Count == 3);

        Assert.Equal(123, vm.Options[2].Value);
        Assert.Equal(
            Loc.Instance[BaseCustomizeSetting.CustomOptionTranslationKey],
            vm.Options[2].DisplayName
        );
    }

    #endregion

    #region Watcher (external edit) flows

    [Fact]
    public async Task Watcher_ExternalChangeToInScope_RemovesCustomOption()
    {
        WriteValue(99);

        var (_, watcher, vm) = CreateVm();
        await vm.LoadStateAsync();
        Assert.Equal(3, vm.Options!.Count);

        WriteValue(2);
        watcher.Raise(TestKeyPath);

        await WaitUntilAsync(() => Equals(vm.CurrentValue, 2) && vm.Options!.Count == 2);

        Assert.Equal(2, vm.CurrentValue);
        Assert.DoesNotContain(vm.Options, o => Equals(o.Value, 99));
    }

    [Fact]
    public async Task Watcher_ExternalChangeToOutOfScope_AddsCustomWithRawValue()
    {
        WriteValue(2);

        var (_, watcher, vm) = CreateVm();
        await vm.LoadStateAsync();
        Assert.Equal(2, vm.Options!.Count);

        WriteValue(99);
        watcher.Raise(TestKeyPath);

        await WaitUntilAsync(() => Equals(vm.CurrentValue, 99) && vm.Options!.Count == 3);

        Assert.Equal(99, vm.Options[2].Value);
        Assert.Equal(
            Loc.Instance[BaseCustomizeSetting.CustomOptionTranslationKey],
            vm.Options[2].DisplayName
        );
    }

    [Fact]
    public async Task Watcher_ExternalChangeToMissing_ShowsNotSetOption()
    {
        WriteValue(2);

        var (_, watcher, vm) = CreateVm();
        await vm.LoadStateAsync();
        Assert.Equal(2, vm.Options!.Count);

        DeleteValue();
        watcher.Raise(TestKeyPath);

        await WaitUntilAsync(
            () =>
                ReferenceEquals(vm.CurrentValue, BaseCustomizeSetting.MissingValueSentinel)
                && vm.Options!.Count == 3
        );

        Assert.Equal(
            Loc.Instance[BaseCustomizeSetting.CustomOptionNotSetTranslationKey],
            vm.Options[2].DisplayName
        );
        Assert.Same(BaseCustomizeSetting.MissingValueSentinel, vm.Options[2].Value);
    }

    [Fact]
    public async Task Watcher_ExternalChangeFromMissingToInScope_RemovesNotSet()
    {
        DeleteValue();

        var (_, watcher, vm) = CreateVm();
        await vm.LoadStateAsync();
        Assert.Equal(3, vm.Options!.Count);

        WriteValue(1);
        watcher.Raise(TestKeyPath);

        await WaitUntilAsync(() => Equals(vm.CurrentValue, 1) && vm.Options!.Count == 2);

        Assert.DoesNotContain(
            vm.Options,
            o => ReferenceEquals(o.Value, BaseCustomizeSetting.MissingValueSentinel)
        );
    }

    [Fact]
    public async Task Watcher_ExternalChangeWhileCustom_UpdatesCustomValue()
    {
        WriteValue(99);

        var (_, watcher, vm) = CreateVm();
        await vm.LoadStateAsync();
        Assert.Equal(99, vm.Options![2].Value);

        WriteValue(123);
        watcher.Raise(TestKeyPath);

        await WaitUntilAsync(() => Equals(vm.CurrentValue, 123) && Equals(vm.Options[2].Value, 123));

        Assert.Equal(3, vm.Options.Count);
        Assert.Equal(
            Loc.Instance[BaseCustomizeSetting.CustomOptionTranslationKey],
            vm.Options[2].DisplayName
        );
    }

    [Fact]
    public async Task Watcher_ExternalChangeFromCustomToInScope_RemovesCustom()
    {
        WriteValue(99);

        var (_, watcher, vm) = CreateVm();
        await vm.LoadStateAsync();
        Assert.Equal(3, vm.Options!.Count);

        WriteValue(1);
        watcher.Raise(TestKeyPath);

        await WaitUntilAsync(() => Equals(vm.CurrentValue, 1) && vm.Options!.Count == 2);

        Assert.DoesNotContain(vm.Options, o => Equals(o.Value, 99));
    }

    #endregion

    #region Not Set ↔ in-scope transitions (round-trip)

    [Fact]
    public async Task RoundTrip_MissingToDeclared_AppliesWritesValue()
    {
        DeleteValue();

        var (setting, _, vm) = CreateVm();
        await vm.LoadStateAsync();

        vm.CurrentValue = 2;
        await WaitUntilAsync(() => Equals(ReadValue(), 2) && !vm.IsLoading);

        Assert.Equal(1, setting.ApplyCount);
        Assert.Equal(2, ReadValue());
        Assert.Equal(2, vm.CurrentValue);
        Assert.Equal(2, vm.Options!.Count);
    }

    [Fact]
    public async Task RoundTrip_ExternalDeleteAfterDeclared_ShowsNotSet()
    {
        WriteValue(1);

        var (_, watcher, vm) = CreateVm();
        await vm.LoadStateAsync();

        DeleteValue();
        watcher.Raise(TestKeyPath);

        await WaitUntilAsync(
            () =>
                ReferenceEquals(vm.CurrentValue, BaseCustomizeSetting.MissingValueSentinel)
                && vm.Options!.Count == 3
        );

        Assert.Equal(1, vm.Options![0].Value);
        Assert.Equal(2, vm.Options[1].Value);
        Assert.Same(BaseCustomizeSetting.MissingValueSentinel, vm.Options[2].Value);
    }

    #endregion

    #region Watcher subscription lifecycle

    [Fact]
    public async Task Dispose_UnwatchesAllPaths()
    {
        WriteValue(1);

        var (_, watcher, vm) = CreateVm();
        await vm.LoadStateAsync();
        Assert.NotEmpty(watcher.WatchedPaths);

        vm.Dispose();

        Assert.Equal(watcher.WatchedPaths.OrderBy(p => p).ToArray(), watcher.UnwatchedPaths.OrderBy(p => p).ToArray());
    }

    #endregion
}
