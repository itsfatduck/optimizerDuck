using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Conditions;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Conditions;
using optimizerDuck.Services.System;

namespace optimizerDuck.Test.Domain.Conditions;

public class ConditionTests
{
    #region Snapshot builder

    private static SystemSnapshot Snapshot(
        string build = "22631",
        GpuVendor gpuVendor = GpuVendor.Unknown,
        CpuVendor cpuVendor = CpuVendor.Unknown,
        double ramGb = 0
    ) =>
        new()
        {
            Cpu = new CpuInfo
            {
                Name = "Test CPU",
                Manufacturer = "TestManufacturer",
                Vendor = cpuVendor,
                Architecture = "64-bit",
                Cores = 8,
                Threads = 16,
                MaxClockMHz = 4000,
                CurrentClockMHz = 4000,
                L2CacheKB = 0,
                L3CacheKB = 0,
            },
            Ram = new RamInfo
            {
                TotalGB = ramGb,
                TotalMB = (long)(ramGb * 1024),
                TotalKB = (long)(ramGb * 1024 * 1024),
                AvailableGB = ramGb,
                UsedPercent = 0,
                UsedGB = 0,
                Modules = [],
            },
            Os = new OsInfo
            {
                Name = "Microsoft Windows 11 Pro",
                Version = "11",
                BuildNumber = build,
                Edition = "Pro",
                Architecture = "64-bit",
                DeviceType = DeviceKind.Desktop,
                InstallDate = "2024-01-01",
                LastBootUpTime = "2024-01-01 00:00",
            },
            Bios = BiosInfo.Unknown,
            Gpus =
                gpuVendor == GpuVendor.Unknown
                    ? []
                    :
                    [
                        new GpuInfo
                        {
                            Name = "Test GPU",
                            DriverVersion = "1.0",
                            Vendor = gpuVendor,
                            MemoryMB = 8192,
                        },
                    ],
            PrimaryGpu = null,
            Disk = DiskInfo.Unknown,
        };

    #endregion

    #region ConditionResult

    [Fact]
    public void ConditionResult_Available_IsNotBlocking()
    {
        Assert.False(ConditionResult.Available.IsBlocking);
        Assert.Equal(ConditionState.Available, ConditionResult.Available.State);
    }

    [Fact]
    public void ConditionResult_Unsupported_IsBlocking()
    {
        var result = ConditionResult.Unsupported(() => "k.Title", () => "k.Description");
        Assert.True(result.IsBlocking);
    }

    [Fact]
    public void ConditionResult_Error_IsNotBlocking()
    {
        var result = ConditionResult.Error();
        Assert.False(result.IsBlocking);
        Assert.Equal(ConditionState.Error, result.State);
    }

    [Fact]
    public void ConditionResult_ResolvesLocalizedTitleAndDescription()
    {
        var result = ConditionResult.Unsupported(
            () => Translations.Condition_Windows11_Title,
            () => Translations.Condition_Windows11_Description
        );

        Assert.False(string.IsNullOrEmpty(result.Title));
        Assert.False(string.IsNullOrEmpty(result.Description));
    }

    #endregion

    #region OS conditions

    [Theory]
    [InlineData("22000")]
    [InlineData("22631")]
    [InlineData("26100")]
    public void Windows11Condition_Win11Builds_Available(string build)
    {
        var result = new Windows11Condition().Evaluate(Snapshot(build: build));
        Assert.Equal(ConditionState.Available, result.State);
    }

    [Fact]
    public void Windows11Condition_DottedBuild_ParsesMajorVersionOnly()
    {
        // Real UBR-suffixed builds (e.g. 24H2 with KB5055627 reports 26100.3915)
        // must be compared by major build number only.
        var result = new Windows11Condition().Evaluate(Snapshot(build: "26100.3915"));
        Assert.Equal(ConditionState.Available, result.State);
    }

    [Theory]
    [InlineData("10240")]
    [InlineData("19045")]
    public void Windows11Condition_Win10Builds_Unsupported(string build)
    {
        var result = new Windows11Condition().Evaluate(Snapshot(build: build));
        Assert.Equal(ConditionState.Unsupported, result.State);
    }

    [Theory]
    [InlineData("10240")]
    [InlineData("19045")]
    public void Windows10Condition_Win10Builds_Available(string build)
    {
        var result = new Windows10Condition().Evaluate(Snapshot(build: build));
        Assert.Equal(ConditionState.Available, result.State);
    }

    [Theory]
    [InlineData("22000")]
    [InlineData("22631")]
    public void Windows10Condition_Win11Builds_Unsupported(string build)
    {
        var result = new Windows10Condition().Evaluate(Snapshot(build: build));
        Assert.Equal(ConditionState.Unsupported, result.State);
    }

    [Theory]
    [InlineData("26100")]
    [InlineData("26120")]
    public void Windows11_24H2OrGreaterCondition_24H2Builds_Available(string build)
    {
        var result = new Windows11_24H2OrGreaterCondition().Evaluate(Snapshot(build: build));
        Assert.Equal(ConditionState.Available, result.State);
    }

    [Theory]
    [InlineData("22631")]
    [InlineData("22000")]
    [InlineData("19045")]
    public void Windows11_24H2OrGreaterCondition_OlderBuilds_Unsupported(string build)
    {
        var result = new Windows11_24H2OrGreaterCondition().Evaluate(Snapshot(build: build));
        Assert.Equal(ConditionState.Unsupported, result.State);
    }

    #endregion

    #region Hardware conditions

    [Fact]
    public void AmdGpuCondition_AmdGpu_Available()
    {
        var result = new AmdGpuCondition().Evaluate(Snapshot(gpuVendor: GpuVendor.AMD));
        Assert.Equal(ConditionState.Available, result.State);
    }

    [Fact]
    public void AmdGpuCondition_NvidiaGpu_Unsupported()
    {
        var result = new AmdGpuCondition().Evaluate(Snapshot(gpuVendor: GpuVendor.NVIDIA));
        Assert.Equal(ConditionState.Unsupported, result.State);
    }

    [Fact]
    public void NvidiaGpuCondition_NvidiaGpu_Available()
    {
        var result = new NvidiaGpuCondition().Evaluate(Snapshot(gpuVendor: GpuVendor.NVIDIA));
        Assert.Equal(ConditionState.Available, result.State);
    }

    [Fact]
    public void NvidiaGpuCondition_AmdGpu_Unsupported()
    {
        var result = new NvidiaGpuCondition().Evaluate(Snapshot(gpuVendor: GpuVendor.AMD));
        Assert.Equal(ConditionState.Unsupported, result.State);
    }

    [Fact]
    public void IntelGpuCondition_IntelGpu_Available()
    {
        var result = new IntelGpuCondition().Evaluate(Snapshot(gpuVendor: GpuVendor.Intel));
        Assert.Equal(ConditionState.Available, result.State);
    }

    [Fact]
    public void IntelCpuCondition_IntelCpu_Available()
    {
        var result = new IntelCpuCondition().Evaluate(Snapshot(cpuVendor: CpuVendor.Intel));
        Assert.Equal(ConditionState.Available, result.State);
    }

    [Fact]
    public void IntelCpuCondition_AmdCpu_Unsupported()
    {
        var result = new IntelCpuCondition().Evaluate(Snapshot(cpuVendor: CpuVendor.AMD));
        Assert.Equal(ConditionState.Unsupported, result.State);
    }

    [Fact]
    public void AmdCpuCondition_AmdCpu_Available()
    {
        var result = new AmdCpuCondition().Evaluate(Snapshot(cpuVendor: CpuVendor.AMD));
        Assert.Equal(ConditionState.Available, result.State);
    }

    [Fact]
    public void IntelCpuCondition_UnknownCpu_FailsOpenToError()
    {
        // An undetected CPU vendor means detection failed, which must never hide the
        // item: the result is non-blocking Error instead of Unsupported.
        var result = new IntelCpuCondition().Evaluate(Snapshot(cpuVendor: CpuVendor.Unknown));
        Assert.Equal(ConditionState.Error, result.State);
        Assert.False(result.IsBlocking);
    }

    [Fact]
    public void SixteenGbRamCondition_16Gb_Available()
    {
        var result = new SixteenGbRamCondition().Evaluate(Snapshot(ramGb: 16));
        Assert.Equal(ConditionState.Available, result.State);
    }

    [Fact]
    public void SixteenGbRamCondition_8Gb_Unsupported()
    {
        var result = new SixteenGbRamCondition().Evaluate(Snapshot(ramGb: 8));
        Assert.Equal(ConditionState.Unsupported, result.State);
    }

    [Fact]
    public void NvidiaGpuCondition_NoGpu_FailsOpenToError()
    {
        // A missing/undetected GPU list means detection failed, which must never hide
        // the item: the result is non-blocking Error instead of Unsupported.
        var result = new NvidiaGpuCondition().Evaluate(Snapshot());
        Assert.Equal(ConditionState.Error, result.State);
        Assert.False(result.IsBlocking);
    }

    #endregion

    #region Registry & Service conditions

    private static readonly Func<string> KTitle = () => "k.Title";
    private static readonly Func<string> KDescription = () => "k.Description";

    private sealed class ExistingKeyCondition : RegistryKeyExistsCondition
    {
        protected override RegistryItem RegistryItem =>
            new(@"HKCU\Software\Microsoft\Windows\CurrentVersion");

        protected override Func<string> Title => KTitle;
        protected override Func<string> Description => KDescription;
    }

    private sealed class MissingKeyCondition : RegistryKeyExistsCondition
    {
        protected override RegistryItem RegistryItem =>
            new(@"HKCU\Software\OptimizerDuck_DefinitelyMissing_Key");

        protected override Func<string> Title => KTitle;
        protected override Func<string> Description => KDescription;
    }

    private sealed class ExistingServiceCondition : ServiceExistsCondition
    {
        protected override string ServiceName => "RpcSs";
        protected override Func<string> Title => KTitle;
        protected override Func<string> Description => KDescription;
    }

    private sealed class MissingServiceCondition : ServiceExistsCondition
    {
        protected override string ServiceName => "OptimizerDuckDefinitelyMissingService";
        protected override Func<string> Title => KTitle;
        protected override Func<string> Description => KDescription;
    }

    [Fact]
    public void RegistryKeyExistsCondition_ExistingKey_Available()
    {
        var result = new ExistingKeyCondition().Evaluate(Snapshot());
        Assert.Equal(ConditionState.Available, result.State);
    }

    [Fact]
    public void RegistryKeyExistsCondition_MissingKey_Unsupported()
    {
        var result = new MissingKeyCondition().Evaluate(Snapshot());
        Assert.Equal(ConditionState.Unsupported, result.State);
    }

    [Fact]
    public void ServiceExistsCondition_ExistingService_Available()
    {
        var result = new ExistingServiceCondition().Evaluate(Snapshot());
        Assert.Equal(ConditionState.Available, result.State);
    }

    [Fact]
    public void ServiceExistsCondition_MissingService_Unsupported()
    {
        var result = new MissingServiceCondition().Evaluate(Snapshot());
        Assert.Equal(ConditionState.Unsupported, result.State);
    }

    #endregion

    #region ConditionEvaluator

    private static ConditionResult Evaluate(Type? type, SystemSnapshot snapshot) =>
        ConditionEvaluator.Evaluate(type, snapshot, NullLogger.Instance);

    [Fact]
    public void ConditionEvaluator_NullType_ReturnsAvailable()
    {
        Assert.Equal(ConditionState.Available, Evaluate(null, Snapshot()).State);
    }

    [Fact]
    public void ConditionEvaluator_ValidType_ReturnsEvaluatedResult()
    {
        var result = Evaluate(typeof(Windows11Condition), Snapshot(build: "19045"));
        Assert.Equal(ConditionState.Unsupported, result.State);
    }

    [Fact]
    public void ConditionEvaluator_UnknownSnapshot_FailOpenToAvailable()
    {
        // An unpopulated snapshot (detection not finished/failed) never hides an item.
        Assert.Equal(
            ConditionState.Available,
            Evaluate(typeof(Windows11Condition), SystemSnapshot.Unknown).State
        );
        Assert.Equal(
            ConditionState.Available,
            Evaluate(typeof(Windows11_24H2OrGreaterCondition), SystemSnapshot.Unknown).State
        );
        Assert.Equal(
            ConditionState.Available,
            Evaluate(typeof(NvidiaGpuCondition), SystemSnapshot.Unknown).State
        );
        Assert.Equal(
            ConditionState.Available,
            Evaluate(typeof(SixteenGbRamCondition), SystemSnapshot.Unknown).State
        );
        Assert.Equal(
            ConditionState.Available,
            Evaluate(typeof(IntelCpuCondition), SystemSnapshot.Unknown).State
        );
    }

    [Fact]
    public void ConditionEvaluator_InvalidType_ReturnsError()
    {
        var result = Evaluate(typeof(string), Snapshot());
        Assert.Equal(ConditionState.Error, result.State);
    }

    [Fact]
    public void ConditionEvaluator_ThrowingCondition_ReturnsError()
    {
        var result = Evaluate(typeof(ThrowingCondition), Snapshot());
        Assert.Equal(ConditionState.Error, result.State);
    }

    [Fact]
    public void ConditionEvaluator_EvaluateAll_AppliesEveryResult()
    {
        var items = new[] { typeof(Windows11Condition), (Type?)null };
        var applied = new List<ConditionResult>();

        ConditionEvaluator.EvaluateAll(
            items,
            t => t,
            (t, r) => applied.Add(r),
            Snapshot(build: "19045"),
            NullLogger.Instance
        );

        Assert.Equal(2, applied.Count);
        Assert.Equal(ConditionState.Unsupported, applied[0].State);
        Assert.Equal(ConditionState.Available, applied[1].State);
    }

    private sealed class ThrowingCondition : ConditionBase
    {
        public override ConditionResult Evaluate(SystemSnapshot snapshot)
        {
            throw new InvalidOperationException("boom");
        }
    }

    #endregion

    #region BaseOptimization condition blocking

    [Fact]
    public void BaseOptimization_UnsupportedAndNotApplied_IsBlocked()
    {
        var optimization = new ConditionedOptimization { Condition = ConditionState.Unsupported };
        Assert.True(optimization.IsConditionBlocked);
    }

    [Fact]
    public void BaseOptimization_UnsupportedAndApplied_IsNotBlocked()
    {
        var optimization = new ConditionedOptimization { Condition = ConditionState.Unsupported };
        optimization.State.IsApplied = true;
        Assert.False(optimization.IsConditionBlocked);
    }

    [Fact]
    public void BaseOptimization_UnsupportedAndHidden_IsNotBlocked()
    {
        var optimization = new ConditionedOptimization { Condition = ConditionState.Unsupported };
        optimization.IsConditionHidden = true;
        Assert.False(optimization.IsConditionBlocked);
    }

    [Fact]
    public void BaseOptimization_Available_IsNotBlocked()
    {
        var optimization = new ConditionedOptimization { Condition = ConditionState.Available };
        Assert.False(optimization.IsConditionBlocked);
    }

    [Fact]
    public void BaseOptimization_ConditionType_ReadFromAttribute()
    {
        var optimization = new ConditionedOptimization();
        Assert.Equal(typeof(Windows11Condition), optimization.ConditionType);
    }

    #endregion

    [Optimization(
        Id = "00000000-0000-0000-0000-000000000001",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System,
        Condition = typeof(Windows11Condition)
    )]
    private sealed class ConditionedOptimization : BaseOptimization
    {
        public ConditionState Condition
        {
            get => ConditionResult.State;
            set =>
                ConditionResult =
                    value == ConditionState.Available
                        ? ConditionResult.Available
                        : ConditionResult.Unsupported(() => "k.Title", () => "k.Description");
        }

        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        ) => Task.FromResult(ApplyResult.True());
    }
}
