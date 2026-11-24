using System.Collections.ObjectModel;
using System.IO;
using System.Resources;
using System.Text;
using System.Windows.Baml2006;
using System.Windows.Threading;
using System.Xaml;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Categories;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.Revert;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.History;
using optimizerDuck.Services.Optimization;
using optimizerDuck.Services.Revert;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.Test.TestDoubles;
using optimizerDuck.UI.Behaviors;
using optimizerDuck.UI.Dialogs;
using optimizerDuck.UI.Pages;
using optimizerDuck.UI.ViewModels.Dialogs;
using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.Test.Services.Optimization;

public class OptimizationMetadataAndStateTests : IDisposable
{
    private readonly List<string> _cleanupPaths = [];

    public void Dispose()
    {
        foreach (var path in _cleanupPaths)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }
    }

    private string TrackRevertFile(Guid id)
    {
        var path = Path.Combine(Shared.RevertDirectory, $"{id}.json");
        _cleanupPaths.Add(path);
        _cleanupPaths.Add(path + ".tmp");
        return path;
    }

    private string TrackHistoryFile(Guid id)
    {
        var path = ChangeRecordStore.PathFor(id);
        _cleanupPaths.Add(path);
        _cleanupPaths.Add(path + ".tmp");
        return path;
    }

    private static RevertManager NewRevertManager() =>
        new(
            NullLogger<RevertManager>.Instance,
            TestShell.New(),
            new PowerPlanService(NullLogger<PowerPlanService>.Instance),
            TimeProvider.System
        );

    private static OptimizationService NewOptimizationService(RevertManager revertManager) =>
        new(
            TestRunner.New(revertManager),
            revertManager,
            NullLoggerFactory.Instance,
            new ContentDialogService(),
            new SystemRestoreService(NullLogger<SystemRestoreService>.Instance),
            NullLogger<OptimizationService>.Instance
        );

    private static SystemInfo PopulatedSnapshot() =>
        SystemInfo.Unknown with
        {
            IsUnknown = false,
            Windows = new WindowsInfo
            {
                BuildNumber = 26100,
                Architecture = Architecture.X64,
                DeviceKind = DeviceKind.Desktop,
            },
        };

    private static async Task WriteDummyRevertAsync(
        RevertManager revertManager,
        Guid id,
        string key,
        CancellationToken cancellationToken
    )
    {
        var changes = new ChangeSet();
        changes.Add(
            "Test",
            "Dummy revert step",
            true,
            new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 0" }
        );
        await revertManager.SaveRevertDataAsync(changes, id, key, cancellationToken);
    }

    private sealed class ProbeStubOptimization(Func<bool?> probeResult) : StubOptimization
    {
        public override Task<bool?> IsSystemAtTargetAsync(
            SystemInfo snapshot,
            IReadOnlyDictionary<string, string>? appliedOptions = null,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(probeResult());

        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        ) => Task.FromResult(ApplyResult.True());
    }

    [Fact]
    public async Task UpdateOptimizationStateAsync_DistinguishesAllFourPresenceStates()
    {
        var revertManager = NewRevertManager();
        var notAppliedId = Guid.NewGuid();
        var appliedId = Guid.NewGuid();
        var alreadyOptimalId = Guid.NewGuid();
        var driftedId = Guid.NewGuid();

        foreach (var id in new[] { notAppliedId, appliedId, alreadyOptimalId, driftedId })
        {
            TrackRevertFile(id);
            TrackHistoryFile(id);
        }

        await WriteDummyRevertAsync(
            revertManager,
            appliedId,
            "AppliedStub",
            TestContext.Current.CancellationToken
        );
        await WriteDummyRevertAsync(
            revertManager,
            driftedId,
            "DriftedStub",
            TestContext.Current.CancellationToken
        );

        var notApplied = new ProbeStubOptimization(() => false) { Id = notAppliedId };
        var applied = new ProbeStubOptimization(() => true) { Id = appliedId };
        var alreadyOptimal = new ProbeStubOptimization(() => true) { Id = alreadyOptimalId };
        var drifted = new ProbeStubOptimization(() => false) { Id = driftedId };

        await OptimizationService.UpdateOptimizationStateAsync(
            PopulatedSnapshot(),
            notApplied,
            applied,
            alreadyOptimal,
            drifted
        );

        Assert.Equal(OptimizationPresence.NotApplied, notApplied.State.Presence);
        Assert.False(notApplied.State.IsApplied);
        Assert.True(notApplied.State.ShowToggle);

        Assert.Equal(OptimizationPresence.Applied, applied.State.Presence);
        Assert.True(applied.State.IsApplied);
        Assert.True(applied.State.HasRevertData);

        Assert.Equal(OptimizationPresence.AlreadyOptimal, alreadyOptimal.State.Presence);
        Assert.True(alreadyOptimal.State.IsAlreadyOptimal);
        Assert.False(alreadyOptimal.State.IsApplied);
        Assert.False(alreadyOptimal.State.ShowToggle);

        Assert.Equal(OptimizationPresence.Drifted, drifted.State.Presence);
        Assert.True(drifted.State.IsDrifted);
        Assert.True(drifted.State.HasRevertData);
        Assert.False(drifted.State.IsApplied);
        Assert.True(drifted.State.ShowToggle);
    }

    [Fact]
    public async Task ClearStaleRevertDataAsync_DeletesRevertFileAndTransitionsToNotApplied()
    {
        var revertManager = NewRevertManager();
        var service = NewOptimizationService(revertManager);
        var id = Guid.NewGuid();
        var revertPath = TrackRevertFile(id);
        TrackHistoryFile(id);

        await WriteDummyRevertAsync(
            revertManager,
            id,
            "DriftedStub",
            TestContext.Current.CancellationToken
        );
        Assert.True(File.Exists(revertPath));

        var opt = new ProbeStubOptimization(() => false) { Id = id };
        await OptimizationService.UpdateOptimizationStateAsync(PopulatedSnapshot(), opt);
        Assert.Equal(OptimizationPresence.Drifted, opt.State.Presence);

        await service.ClearStaleRevertDataAsync(opt, PopulatedSnapshot());

        Assert.False(File.Exists(revertPath));
        Assert.Equal(OptimizationPresence.NotApplied, opt.State.Presence);
        Assert.False(opt.State.HasRevertData);
    }

    [Fact]
    public async Task ScanLegacyRevertsAsync_DetectsOrphanedIdsAndAdaptsForRevert()
    {
        var revertManager = NewRevertManager();
        var legacyId = Guid.NewGuid();
        var activeId = Guid.NewGuid();
        var legacyPath = TrackRevertFile(legacyId);
        TrackRevertFile(activeId);
        TrackHistoryFile(legacyId);

        await WriteDummyRevertAsync(
            revertManager,
            legacyId,
            "RetiredTestOptimization",
            TestContext.Current.CancellationToken
        );
        await WriteDummyRevertAsync(
            revertManager,
            activeId,
            "ActiveTestOptimization",
            TestContext.Current.CancellationToken
        );

        ChangeRecordStore.TryWrite(
            new ChangeRecord
            {
                Id = legacyId,
                OptimizationKey = "RetiredTestOptimization",
                LogName = "Retired Optimization Display",
                AppliedAt = DateTime.Now,
                Outcome = "Success",
            }
        );

        var activeIds = new HashSet<Guid> { activeId };
        var found = await OptimizationRegistry.ScanLegacyRevertsAsync(activeIds);

        var legacyItem = Assert.Single(found, i => i.Id == legacyId);
        Assert.DoesNotContain(found, i => i.Id == activeId);
        Assert.Equal("Retired Optimization Display", legacyItem.DisplayName);
        Assert.Equal(1, legacyItem.StepCount);

        var revertResult = await revertManager.RevertAsync(
            legacyItem.AsOptimization(),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.True(revertResult.Success, revertResult.Message);
        Assert.False(File.Exists(legacyPath));
    }

    [Fact]
    public void ChangeRecord_SelectedOptions_RoundTripsAndHandlesLegacyJsonWithoutField()
    {
        var withOptionsId = Guid.NewGuid();
        var legacyJsonId = Guid.NewGuid();
        TrackHistoryFile(withOptionsId);
        var legacyPath = TrackHistoryFile(legacyJsonId);

        var original = new ChangeRecord
        {
            Id = withOptionsId,
            OptimizationKey = "ConfigurableTest",
            LogName = "Configurable Test",
            AppliedAt = DateTime.Now,
            Outcome = "Success",
            SelectedOptions = new Dictionary<string, string>
            {
                ["Mode"] = "FastStartupOnly",
                ["IncludeSensors"] = "false",
            },
        };

        ChangeRecordStore.TryWrite(original);
        var roundTripped = ChangeRecordStore.TryRead(withOptionsId);

        Assert.NotNull(roundTripped?.SelectedOptions);
        Assert.Equal("FastStartupOnly", roundTripped.SelectedOptions["Mode"]);
        Assert.Equal("false", roundTripped.SelectedOptions["IncludeSensors"]);

        // Legacy JSON written before SelectedOptions existed must still deserialize cleanly.
        Directory.CreateDirectory(Shared.HistoryDirectory);
        File.WriteAllText(
            legacyPath,
            $$"""
            {
              "Id": "{{legacyJsonId}}",
              "OptimizationKey": "OldRecord",
              "LogName": "Old Record",
              "Outcome": "Success",
              "Steps": []
            }
            """,
            Encoding.UTF8
        );

        var legacy = ChangeRecordStore.TryRead(legacyJsonId);
        Assert.NotNull(legacy);
        Assert.Null(legacy.SelectedOptions);
    }

    [Fact]
    public async Task OperationRunner_RunAsync_ForwardsSelectedOptionsIntoContext()
    {
        var revertManager = NewRevertManager();
        var runner = TestRunner.New(revertManager);
        var id = Guid.NewGuid();
        TrackRevertFile(id);
        TrackHistoryFile(id);

        string? observedMode = null;
        var request = new OperationRequest(
            new OperationSubject(id, "ConfigurableRunTest", "Configurable Run Test"),
            "Configurable Run Test",
            NullLogger.Instance,
            RevertPersistence.Disabled,
            (_, ctx) =>
            {
                observedMode = ctx.Options.GetString("Mode", "Fallback");
                ctx.Changes.AddSkip("Test", "Observed option");
                return Task.FromResult(ctx.Changes.ToApplyResult());
            },
            new Dictionary<string, string> { ["Mode"] = "CustomChoice" }
        );

        var result = await runner.RunAsync(
            request,
            new Progress<ProcessingProgress>(),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(OptimizationSuccessResult.NothingToDo, result.Status);
        Assert.Equal("CustomChoice", observedMode);
    }

    [Optimization(
        Id = "99999999-1111-2222-3333-444444444444",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System
    )]
    private sealed class PolicyRegistryStub : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [new(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Example", "Flag", 1)];
    }

    [Optimization(
        Id = "99999999-1111-2222-3333-555555555555",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System
    )]
    private sealed class NonPolicyRegistryStub : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [new(@"HKCU\Control Panel\Desktop", "Example", 1)];
    }

    [Optimization(
        Id = "99999999-1111-2222-3333-666666666666",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System
    )]
    private sealed class DeclarativeProbeStub(string testPath) : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } = [new(testPath, "KeepValue", 1)];

        protected override RegistryItem[] DeleteRegistryValues { get; } =
        [new(testPath, "DeleteMe")];
    }

    [Fact]
    public async Task DeclarativeOptimization_AutoDetectsGroupPolicyAndCombinesProbes()
    {
        var policyOpt = new PolicyRegistryStub();
        var nonPolicyOpt = new NonPolicyRegistryStub();

        Assert.True(policyOpt.SetsGroupPolicy);
        Assert.False(nonPolicyOpt.SetsGroupPolicy);

        var relPath = $@"Software\TestOptimizerDuck_Declarative_{Guid.NewGuid():N}";
        var fullPath = $@"HKCU\{relPath}";
        var opt = new DeclarativeProbeStub(fullPath) { OwnerType = typeof(Performance) };

        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(relPath, writable: true))
            {
                key.SetValue("KeepValue", 1, RegistryValueKind.DWord);
                key.SetValue("DeleteMe", 1, RegistryValueKind.DWord);
            }

            Assert.Equal(
                false,
                await opt.IsSystemAtTargetAsync(
                    PopulatedSnapshot(),
                    cancellationToken: TestContext.Current.CancellationToken
                )
            );

            using (var key = Registry.CurrentUser.CreateSubKey(relPath, writable: true))
            {
                key.SetValue("KeepValue", 0, RegistryValueKind.DWord);
            }

            var runner = TestRunner.New(NewRevertManager());
            var applyResult = await runner.RunAsync(
                new OperationRequest(
                    new OperationSubject(opt.Id, opt.OptimizationKey, opt.OptimizationKey),
                    opt.OptimizationKey,
                    NullLogger.Instance,
                    RevertPersistence.Disabled,
                    opt.ApplyAsync
                ),
                new Progress<ProcessingProgress>(),
                TestContext.Current.CancellationToken
            );

            Assert.Equal(OptimizationSuccessResult.Success, applyResult.Status);
            Assert.Equal(
                true,
                await opt.IsSystemAtTargetAsync(
                    PopulatedSnapshot(),
                    cancellationToken: TestContext.Current.CancellationToken
                )
            );
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(relPath, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void BaseOptimization_AutoDetectsTradeOffAndFallbackDetailsFromResources()
    {
        var withTradeOff = new Performance.DisableMemoryIntegrityHvci
        {
            OwnerType = typeof(Performance),
        };
        var withoutTradeOff = new UserExperience.DisableExplorerStartupDelay
        {
            OwnerType = typeof(UserExperience),
        };

        Assert.True(withTradeOff.HasTradeOff);
        Assert.False(string.IsNullOrWhiteSpace(withTradeOff.TradeOff));

        Assert.False(withoutTradeOff.HasTradeOff);
        Assert.Equal(string.Empty, withoutTradeOff.TradeOff);
        Assert.Equal(withoutTradeOff.ShortDescription, withoutTradeOff.DetailedDescription);

        Assert.True(
            Loc.Instance.HasKey("Optimizer.Performance.DisableMemoryIntegrityHvci.TradeOff")
        );
        Assert.False(
            Loc.Instance.HasKey("Optimizer.UserExperience.DisableExplorerStartupDelay.TradeOff")
        );
        Assert.True(Loc.Instance.TryGet("Button.Ok", out var ok));
        Assert.False(string.IsNullOrEmpty(ok));
        Assert.False(Loc.Instance.TryGet("NonExistent.Key.For.Test", out var missing));
        Assert.Equal(string.Empty, missing);
    }

    [Fact]
    public async Task StateProbe_ActivePowerSchemeIs_QueriesLiveActiveSchemeAndIgnoresStaleSnapshot()
    {
        var activeScheme = PowerPlanService.QueryActiveSchemeId();
        Assert.True(activeScheme.HasValue);

        Assert.Null(StateProbe.ActivePowerSchemeIs(Guid.Empty));
        Assert.Equal(true, StateProbe.ActivePowerSchemeIs(activeScheme.Value));
        Assert.Equal(false, StateProbe.ActivePowerSchemeIs(Guid.NewGuid()));

        var optimizerDuckPlanId = Guid.Parse(Shared.PowerPlanGUID);
        var expected = activeScheme.Value == optimizerDuckPlanId;
        var staleSchemeId = expected ? Guid.NewGuid() : optimizerDuckPlanId;

        var staleSnapshot = PopulatedSnapshot() with
        {
            Power = new PowerInfo { SchemeId = staleSchemeId, SchemeName = "Stale Cached Scheme" },
        };

        var opt = new PowerManagement.InstallOptimizerDuckPowerPlan
        {
            OwnerType = typeof(PowerManagement),
        };
        var actual = await opt.IsSystemAtTargetAsync(
            staleSnapshot,
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task UpdateOptimizationStateAsync_PreservesSessionAlreadyOptimalWhenProbeIsNullAndClearsOnFalse()
    {
        var id = Guid.NewGuid();
        TrackRevertFile(id);
        TrackHistoryFile(id);

        bool? currentProbe = null;
        var opt = new ProbeStubOptimization(() => currentProbe) { Id = id };

        opt.State.MarkSessionAlreadyOptimal();
        Assert.Equal(OptimizationPresence.AlreadyOptimal, opt.State.Presence);
        Assert.True(opt.State.IsAlreadyOptimal);
        Assert.False(opt.State.ShowToggle);

        await OptimizationService.UpdateOptimizationStateAsync(PopulatedSnapshot(), opt);

        Assert.Equal(OptimizationPresence.AlreadyOptimal, opt.State.Presence);
        Assert.True(opt.State.IsAlreadyOptimal);
        Assert.False(opt.State.ShowToggle);

        currentProbe = false;
        await OptimizationService.UpdateOptimizationStateAsync(PopulatedSnapshot(), opt);

        Assert.Equal(OptimizationPresence.NotApplied, opt.State.Presence);
        Assert.False(opt.State.IsAlreadyOptimal);
        Assert.True(opt.State.ShowToggle);
    }

    private sealed class CapturingOptionsStubOptimization : StubOptimization
    {
        public IReadOnlyDictionary<string, string>? LastAppliedOptions { get; private set; }

        public override Task<bool?> IsSystemAtTargetAsync(
            SystemInfo snapshot,
            IReadOnlyDictionary<string, string>? appliedOptions = null,
            CancellationToken cancellationToken = default
        )
        {
            LastAppliedOptions = appliedOptions;
            return Task.FromResult<bool?>(appliedOptions is not null);
        }

        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        ) => Task.FromResult(ApplyResult.True());
    }

    [Fact]
    public async Task UpdateOptimizationStateAsync_OnlyPassesSelectedOptionsWhenRevertDataExists()
    {
        var revertManager = NewRevertManager();
        var id = Guid.NewGuid();
        TrackRevertFile(id);
        TrackHistoryFile(id);

        ChangeRecordStore.TryWrite(
            new ChangeRecord
            {
                Id = id,
                OptimizationKey = "OptionsGuardStub",
                LogName = "Options Guard Stub",
                AppliedAt = DateTime.Now,
                Outcome = "NothingToDo",
                SelectedOptions = new Dictionary<string, string> { ["GroupA"] = "true" },
            }
        );

        var opt = new CapturingOptionsStubOptimization { Id = id };

        await OptimizationService.UpdateOptimizationStateAsync(PopulatedSnapshot(), opt);
        Assert.Null(opt.LastAppliedOptions);
        Assert.Equal(OptimizationPresence.NotApplied, opt.State.Presence);

        await WriteDummyRevertAsync(
            revertManager,
            id,
            "OptionsGuardStub",
            TestContext.Current.CancellationToken
        );

        await OptimizationService.UpdateOptimizationStateAsync(PopulatedSnapshot(), opt);
        Assert.NotNull(opt.LastAppliedOptions);
        Assert.Equal("true", opt.LastAppliedOptions["GroupA"]);
        Assert.Equal(OptimizationPresence.Applied, opt.State.Presence);
    }

    [Fact]
    public void OptimizationSource_From_InfersMicrosoftDocsAndReferenceKinds()
    {
        var learn = OptimizationSource.From(
            "Learn",
            "https://learn.microsoft.com/en-us/windows/client-management/manage-recall"
        );
        var support = OptimizationSource.From(
            "Support",
            "https://support.microsoft.com/en-us/windows/example"
        );
        var github = OptimizationSource.From(
            "WinUtil",
            "https://github.com/ChrisTitusTech/winutil/pull/5170"
        );
        var benchmark = OptimizationSource.From(
            "Benchmark",
            "https://learn.microsoft.com/en-us/example",
            SourceKind.Benchmark
        );

        Assert.Equal(SourceKind.MicrosoftDocs, learn.Kind);
        Assert.Equal(SourceKind.MicrosoftDocs, support.Kind);
        Assert.Equal(SourceKind.Reference, github.Kind);
        Assert.Equal(SourceKind.Benchmark, benchmark.Kind);
    }

    [Fact]
    public void ServiceItem_FactoryHelpers_UseDowngradeOnlyMode()
    {
        var disabled = ServiceItem.Disable("Fax");
        var manual = ServiceItem.Manual("CscService");

        Assert.Equal(ServiceStartupType.Disabled, disabled.StartupType);
        Assert.Equal(ServiceChangeMode.DowngradeOnly, disabled.Mode);
        Assert.Equal(ServiceStartupType.Manual, manual.StartupType);
        Assert.Equal(ServiceChangeMode.DowngradeOnly, manual.Mode);
    }

    [Fact]
    public void BaseOptimization_Prefix_ReturnsRawKeyWithoutResourceLookup()
    {
        var opt = new UserExperience.DisableExplorerStartupDelay
        {
            OwnerType = typeof(UserExperience),
        };

        Assert.Equal("Optimizer.UserExperience.DisableExplorerStartupDelay", opt.Prefix);
        Assert.Equal(
            "Optimizer.UserExperience.DisableExplorerStartupDelay.Progress",
            opt.ProgressPrefix
        );
        Assert.Equal("Optimizer.UserExperience.DisableExplorerStartupDelay.Error", opt.ErrorPrefix);
    }

    [Fact]
    public async Task OptimizationRegistry_LegacyRevertsChanged_FiresOnRefreshAndRemove()
    {
        var registry = new OptimizationRegistry(NullLoggerFactory.Instance);
        await registry.PreloadOptimizationsAsync();

        var revertManager = NewRevertManager();
        var legacyId = Guid.NewGuid();
        TrackRevertFile(legacyId);
        TrackHistoryFile(legacyId);

        var eventCount = 0;
        registry.LegacyRevertsChanged += () => eventCount++;

        await WriteDummyRevertAsync(
            revertManager,
            legacyId,
            "RetiredChangedEventTest",
            TestContext.Current.CancellationToken
        );

        await registry.RefreshLegacyRevertsAsync();
        Assert.Equal(1, eventCount);
        Assert.Contains(registry.LegacyRevertItems, item => item.Id == legacyId);

        registry.RemoveLegacyRevertItem(legacyId);
        Assert.Equal(2, eventCount);
        Assert.DoesNotContain(registry.LegacyRevertItems, item => item.Id == legacyId);
    }

    [Fact]
    public Task OptimizeViewModel_SyncLegacyTab_TracksBadgeAndFiresRemovalWhenEmpty() =>
        RunInStaThreadAsync(async () =>
        {
            var registry = new OptimizationRegistry(NullLoggerFactory.Instance);
            await registry.PreloadOptimizationsAsync();

            var baseLegacyCount = registry.LegacyRevertItems.Count;
            var vm = new OptimizeViewModel(registry);
            var navView = new NavigationView();
            NavigationViewMenuItemsBehavior.SetItemsSource(navView, vm.OptimizationCategories);

            var removalEvents = 0;
            vm.LegacyTabRemoved += () => removalEvents++;

            await vm.OnNavigatedToAsync();

            var revertManager = NewRevertManager();
            var legacyIdA = Guid.NewGuid();
            var legacyIdB = Guid.NewGuid();
            TrackRevertFile(legacyIdA);
            TrackRevertFile(legacyIdB);
            TrackHistoryFile(legacyIdA);
            TrackHistoryFile(legacyIdB);

            await WriteDummyRevertAsync(
                revertManager,
                legacyIdA,
                "LegacyTabTestA",
                TestContext.Current.CancellationToken
            );
            await registry.RefreshLegacyRevertsAsync();

            var legacyTab = Assert.Single(
                vm.OptimizationCategories,
                item => item.TargetPageType == typeof(LegacyRevertsPage)
            );
            Assert.Null(legacyTab.Icon);
            Assert.Contains(legacyTab, navView.MenuItems.Cast<object>());
            Assert.IsType<InfoBadge>(legacyTab.InfoBadge);
            Assert.Equal((baseLegacyCount + 1).ToString(), ((InfoBadge)legacyTab.InfoBadge!).Value);

            await WriteDummyRevertAsync(
                revertManager,
                legacyIdB,
                "LegacyTabTestB",
                TestContext.Current.CancellationToken
            );
            await registry.RefreshLegacyRevertsAsync();

            Assert.Equal((baseLegacyCount + 2).ToString(), ((InfoBadge)legacyTab.InfoBadge!).Value);

            registry.RemoveLegacyRevertItem(legacyIdA);
            Assert.Equal((baseLegacyCount + 1).ToString(), ((InfoBadge)legacyTab.InfoBadge!).Value);

            registry.RemoveLegacyRevertItem(legacyIdB);

            if (baseLegacyCount == 0)
            {
                Assert.DoesNotContain(
                    vm.OptimizationCategories,
                    item => item.TargetPageType == typeof(LegacyRevertsPage)
                );
                Assert.DoesNotContain(legacyTab, navView.MenuItems.Cast<object>());
                Assert.Equal(1, removalEvents);
            }
            else
            {
                Assert.Equal(baseLegacyCount.ToString(), ((InfoBadge)legacyTab.InfoBadge!).Value);
            }

            // Always verify 1 -> 0 NavigationView removal independently of pre-existing host revert files.
            var isolatedNav = new NavigationView();
            var isolatedTabs = new ObservableCollection<NavigationViewItem>();
            NavigationViewMenuItemsBehavior.SetItemsSource(isolatedNav, isolatedTabs);
            var tempTab = new NavigationViewItem { Content = "Legacy" };
            isolatedTabs.Add(tempTab);
            Assert.Contains(tempTab, isolatedNav.MenuItems.Cast<object>());
            isolatedTabs.Remove(tempTab);
            Assert.DoesNotContain(tempTab, isolatedNav.MenuItems.Cast<object>());
        });

    [Fact]
    public void OptimizationOptionsViewModel_ReconfigureAllOff_OffersRevertInsteadOfBlocking()
    {
        var opt = new BloatwareAndServices.DisableOptionalFeatureServices
        {
            OwnerType = typeof(BloatwareAndServices),
        };
        IReadOnlyList<OptimizationOption> options =
        [
            new()
            {
                Key = "OptA",
                Kind = OptimizationOptionKind.Toggle,
                LabelKey = "Optimizer.Options.Enabled",
                DefaultValue = "false",
            },
            new()
            {
                Key = "OptB",
                Kind = OptimizationOptionKind.Toggle,
                LabelKey = "Optimizer.Options.Disabled",
                DefaultValue = "false",
            },
        ];

        var applyVm = new OptimizationOptionsViewModel(
            opt,
            options,
            requiresAtLeastOneSelection: true,
            isReconfiguring: false
        );
        Assert.False(applyVm.CanApply);
        Assert.True(applyVm.ShowSelectAtLeastOneHint);
        Assert.False(applyVm.WillRevertOnConfirm);
        Assert.False(applyVm.ShowWillRevertHint);
        Assert.Equal(ControlAppearance.Primary, applyVm.PrimaryButtonAppearance);

        var reconfigVm = new OptimizationOptionsViewModel(
            opt,
            options,
            requiresAtLeastOneSelection: true,
            isReconfiguring: true
        );
        Assert.True(reconfigVm.CanApply);
        Assert.False(reconfigVm.ShowSelectAtLeastOneHint);
        Assert.True(reconfigVm.WillRevertOnConfirm);
        Assert.True(reconfigVm.ShowWillRevertHint);
        Assert.Equal(ControlAppearance.Caution, reconfigVm.PrimaryButtonAppearance);
        Assert.Equal(Loc.Instance["Optimizer.Options.Revert"], reconfigVm.PrimaryButtonText);
        Assert.Equal(OptimizationOptionsAction.Revert, reconfigVm.BuildConfirmResult().Action);

        reconfigVm.Options[0].BoolValue = true;
        Assert.False(reconfigVm.WillRevertOnConfirm);
        Assert.False(reconfigVm.ShowWillRevertHint);
        Assert.Equal(ControlAppearance.Primary, reconfigVm.PrimaryButtonAppearance);
        Assert.Equal(Loc.Instance["Optimizer.Options.Apply"], reconfigVm.PrimaryButtonText);
        var applyResult = reconfigVm.BuildConfirmResult();
        Assert.Equal(OptimizationOptionsAction.Apply, applyResult.Action);
        Assert.Equal("true", applyResult.SelectedOptions!["OptA"]);

        var optionalReconfigVm = new OptimizationOptionsViewModel(
            opt,
            options,
            requiresAtLeastOneSelection: false,
            isReconfiguring: true
        );
        Assert.False(optionalReconfigVm.WillRevertOnConfirm);
        Assert.Equal(
            OptimizationOptionsAction.Apply,
            optionalReconfigVm.BuildConfirmResult().Action
        );
    }

    [Fact]
    public void OptimizationOptionsDialog_ContainsNoNestedScrollViewer()
    {
        var assembly = typeof(OptimizationOptionsDialog).Assembly;
        var resourceManager = new ResourceManager($"{assembly.GetName().Name}.g", assembly);
        using var resourceSet = resourceManager.GetResourceSet(
            System.Globalization.CultureInfo.InvariantCulture,
            createIfNotExists: true,
            tryParents: false
        );
        Assert.NotNull(resourceSet);

        var stream = resourceSet.GetObject("ui/dialogs/optimizationoptionsdialog.baml") as Stream;
        Assert.NotNull(stream);

        using var bamlReader = new Baml2006Reader(stream);
        var scrollViewerCount = 0;
        while (bamlReader.Read())
        {
            if (
                bamlReader.NodeType == XamlNodeType.StartObject
                && bamlReader.Type?.UnderlyingType is { } underlyingType
                && typeof(System.Windows.Controls.ScrollViewer).IsAssignableFrom(underlyingType)
            )
            {
                scrollViewerCount++;
            }
        }

        Assert.Equal(0, scrollViewerCount);
    }

    [Fact]
    public void OptimizationOptionsViewModel_AlreadySetExternally_LocksToggleAndPreventsNothingToDo()
    {
        var opt = new BloatwareAndServices.DisableOptionalFeatureServices
        {
            OwnerType = typeof(BloatwareAndServices),
        };

        IReadOnlyList<OptimizationOption> partialExternal =
        [
            new()
            {
                Key = "OptA",
                Kind = OptimizationOptionKind.Toggle,
                LabelKey = "Optimizer.Options.Enabled",
                TradeOffKey = "Optimizer.Options.Disabled",
                DefaultValue = "true",
                IsAlreadyAppliedHint = true,
            },
            new()
            {
                Key = "OptB",
                Kind = OptimizationOptionKind.Toggle,
                LabelKey = "Optimizer.Options.Disabled",
                DefaultValue = "false",
                IsAlreadyAppliedHint = false,
            },
        ];

        var applyVm = new OptimizationOptionsViewModel(
            opt,
            partialExternal,
            requiresAtLeastOneSelection: true,
            isReconfiguring: false
        );
        var optA = applyVm.Options[0];
        var optB = applyVm.Options[1];

        Assert.True(optA.IsLockedAlreadySet);
        Assert.False(optA.IsInteractiveToggle);
        Assert.False(optA.ShowTradeOff);
        Assert.False(optA.BoolValue);
        Assert.False(applyVm.CanApply);
        Assert.True(applyVm.ShowSelectAtLeastOneHint);
        Assert.False(applyVm.ShowAllAlreadySetHint);

        optA.BoolValue = true;
        Assert.False(optA.BoolValue);
        Assert.False(applyVm.CanApply);

        optB.BoolValue = true;
        Assert.True(applyVm.CanApply);
        var selectedPartial = applyVm.BuildSelectedOptions();
        Assert.Equal("false", selectedPartial["OptA"]);
        Assert.Equal("true", selectedPartial["OptB"]);

        IReadOnlyList<OptimizationOption> allExternal =
        [
            new()
            {
                Key = "OptA",
                Kind = OptimizationOptionKind.Toggle,
                LabelKey = "Optimizer.Options.Enabled",
                DefaultValue = "true",
                IsAlreadyAppliedHint = true,
            },
            new()
            {
                Key = "OptB",
                Kind = OptimizationOptionKind.Toggle,
                LabelKey = "Optimizer.Options.Disabled",
                DefaultValue = "true",
                IsAlreadyAppliedHint = true,
            },
        ];

        var allLockedRequiredVm = new OptimizationOptionsViewModel(
            opt,
            allExternal,
            requiresAtLeastOneSelection: true,
            isReconfiguring: false
        );
        Assert.True(allLockedRequiredVm.AllOptionsAlreadySet);
        Assert.False(allLockedRequiredVm.CanApply);
        Assert.True(allLockedRequiredVm.ShowAllAlreadySetHint);
        Assert.False(allLockedRequiredVm.ShowSelectAtLeastOneHint);

        var allLockedOptionalVm = new OptimizationOptionsViewModel(
            opt,
            [allExternal[0]],
            requiresAtLeastOneSelection: false,
            isReconfiguring: false
        );
        Assert.True(allLockedOptionalVm.AllOptionsAlreadySet);
        Assert.True(allLockedOptionalVm.CanApply);
        Assert.False(allLockedOptionalVm.ShowAllAlreadySetHint);
        Assert.Equal("false", allLockedOptionalVm.BuildSelectedOptions()["OptA"]);

        var previousSelections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OptA"] = "true",
            ["OptB"] = "false",
        };
        IReadOnlyList<OptimizationOption> mixedOwnership =
        [
            new()
            {
                Key = "OptA",
                Kind = OptimizationOptionKind.Toggle,
                LabelKey = "Optimizer.Options.Enabled",
                DefaultValue = "true",
                IsAlreadyAppliedHint = true,
            },
            new()
            {
                Key = "OptB",
                Kind = OptimizationOptionKind.Toggle,
                LabelKey = "Optimizer.Options.Disabled",
                DefaultValue = "false",
                IsAlreadyAppliedHint = true,
            },
        ];

        var reconfigVm = new OptimizationOptionsViewModel(
            opt,
            mixedOwnership,
            requiresAtLeastOneSelection: true,
            isReconfiguring: true,
            previousSelections: previousSelections
        );
        Assert.False(reconfigVm.Options[0].IsLockedAlreadySet);
        Assert.True(reconfigVm.Options[0].IsInteractiveToggle);
        Assert.True(reconfigVm.Options[0].BoolValue);

        Assert.True(reconfigVm.Options[1].IsLockedAlreadySet);
        Assert.False(reconfigVm.Options[1].IsInteractiveToggle);
        Assert.False(reconfigVm.Options[1].BoolValue);

        reconfigVm.Options[0].BoolValue = false;
        Assert.True(reconfigVm.WillRevertOnConfirm);
        Assert.Equal(OptimizationOptionsAction.Revert, reconfigVm.BuildConfirmResult().Action);
    }

    [Fact]
    public void OptimizationDetailsViewModel_SelectedOptions_HiddenWhenNotAppliedAndStyledWhenApplied()
    {
        var stubId = Guid.NewGuid();
        TrackHistoryFile(stubId);
        var stub = new ConfigurableDetailsStub { Id = stubId };
        var snackbar = new NoopSnackbarService();

        var selected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ToggleOn"] = "true",
            ["ToggleOff"] = "false",
            ["Mode"] = "Fast",
        };
        var applyRecord = ChangeRecord.From(
            new ChangeSet(),
            stub,
            nameof(OptimizationSuccessResult.Success),
            selected
        );

        ChangeRecordStore.TryWrite(applyRecord, NullLogger.Instance);
        stub.State.IsApplied = false;
        stub.State.HasRevertData = false;
        var unappliedVm = new OptimizationDetailsViewModel(
            stub,
            SystemInfo.Unknown,
            snackbar,
            NullLogger.Instance
        );
        Assert.False(unappliedVm.HasSelectedOptions);
        Assert.Empty(unappliedVm.SelectedOptions);

        ChangeRecordStore.TryWrite(applyRecord.Reverted(DateTime.Now), NullLogger.Instance);
        stub.State.IsApplied = false;
        stub.State.HasRevertData = false;
        var revertedVm = new OptimizationDetailsViewModel(
            stub,
            SystemInfo.Unknown,
            snackbar,
            NullLogger.Instance
        );
        Assert.False(revertedVm.HasSelectedOptions);

        ChangeRecordStore.TryWrite(applyRecord, NullLogger.Instance);
        stub.State.IsApplied = true;
        stub.State.HasRevertData = true;
        var appliedVm = new OptimizationDetailsViewModel(
            stub,
            SystemInfo.Unknown,
            snackbar,
            NullLogger.Instance
        );
        Assert.True(appliedVm.HasSelectedOptions);
        Assert.Equal(3, appliedVm.SelectedOptions.Count);

        Assert.Equal(SelectedOptionState.Enabled, appliedVm.SelectedOptions[0].State);
        Assert.Equal(SymbolRegular.CheckmarkCircle24, appliedVm.SelectedOptions[0].Icon);
        Assert.Equal(Loc.Instance["Optimizer.Options.Enabled"], appliedVm.SelectedOptions[0].Value);

        Assert.Equal(SelectedOptionState.Disabled, appliedVm.SelectedOptions[1].State);
        Assert.Equal(SymbolRegular.SubtractCircle24, appliedVm.SelectedOptions[1].Icon);
        Assert.Equal(
            Loc.Instance["Optimizer.Options.Disabled"],
            appliedVm.SelectedOptions[1].Value
        );

        Assert.Equal(SelectedOptionState.Choice, appliedVm.SelectedOptions[2].State);
        Assert.Equal(SymbolRegular.CheckmarkCircle24, appliedVm.SelectedOptions[2].Icon);
        Assert.Equal(Loc.Instance["Common.Filter.All"], appliedVm.SelectedOptions[2].Value);

        ChangeRecordStore.TryWrite(applyRecord.Reverted(DateTime.Now), NullLogger.Instance);
        var revertedResidualVm = new OptimizationDetailsViewModel(
            stub,
            SystemInfo.Unknown,
            snackbar,
            NullLogger.Instance
        );
        Assert.False(revertedResidualVm.HasSelectedOptions);
    }

    [Fact]
    public void LegacyRevertItem_UpdatesLocalizedSummariesOnLanguageChange()
    {
        var originalCulture = Loc.CurrentCulture;
        try
        {
            Loc.Instance.ChangeCulture(System.Globalization.CultureInfo.GetCultureInfo("en-US"));
            var appliedAt = new DateTime(2026, 5, 1, 14, 30, 0);
            var item = new LegacyRevertItem(
                Guid.NewGuid(),
                "LegacyKey",
                "Legacy Name",
                appliedAt,
                3
            );

            var notified = false;
            item.PropertyChanged += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.PropertyName))
                    notified = true;
            };

            var enSteps = item.StepsSummary;
            var enApplied = item.AppliedAtSummary;

            Loc.Instance.ChangeCulture(System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));

            Assert.True(notified);
            Assert.NotEqual(enSteps, item.StepsSummary);
            Assert.NotEqual(enApplied, item.AppliedAtSummary);
            Assert.Equal(Loc.Instance["Optimizer.Legacy.StepCount", 3], item.StepsSummary);
            Assert.Equal(
                Loc.Instance[
                    "Optimizer.Legacy.AppliedAt",
                    appliedAt.ToString("g", Loc.CurrentCulture)
                ],
                item.AppliedAtSummary
            );
        }
        finally
        {
            Loc.Instance.ChangeCulture(originalCulture);
        }
    }

    private sealed class ConfigurableDetailsStub : StubOptimization, IConfigurableOptimization
    {
        public IReadOnlyList<OptimizationOption> GetOptions(
            SystemInfo snapshot,
            IReadOnlyDictionary<string, string>? previousSelections = null
        ) =>
            [
                new()
                {
                    Key = "ToggleOn",
                    Kind = OptimizationOptionKind.Toggle,
                    LabelKey = "Optimizer.Options.Enabled",
                    DefaultValue = "true",
                },
                new()
                {
                    Key = "ToggleOff",
                    Kind = OptimizationOptionKind.Toggle,
                    LabelKey = "Optimizer.Options.Disabled",
                    DefaultValue = "false",
                },
                new()
                {
                    Key = "Mode",
                    Kind = OptimizationOptionKind.SingleChoice,
                    LabelKey = "Common.Filter",
                    DefaultValue = "Fast",
                    Choices = [new("Fast", "Common.Filter.All")],
                },
            ];

        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        ) => Task.FromResult(context.Changes.ToApplyResult());
    }

    private sealed class NoopSnackbarService : ISnackbarService
    {
        public TimeSpan DefaultTimeOut { get; set; } = TimeSpan.FromSeconds(3);

        public SnackbarPresenter? GetSnackbarPresenter() => null;

        public void SetSnackbarPresenter(SnackbarPresenter presenter) { }

        public void Show(
            string title,
            string message,
            ControlAppearance appearance,
            IconElement? icon,
            TimeSpan timeout
        ) { }
    }

    private static Task RunInStaThreadAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource();

        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(dispatcher)
            );
            var frame = new DispatcherFrame();

            dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await action();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
                finally
                {
                    frame.Continue = false;
                }
            });

            Dispatcher.PushFrame(frame);
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return tcs.Task;
    }
}
