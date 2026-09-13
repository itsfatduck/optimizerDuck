using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Services.System;

namespace optimizerDuck.Test.Services;

public class SystemInfoTests
{
    [Theory]
    [InlineData(22000, true)]
    [InlineData(22631, true)]
    [InlineData(26100, true)]
    [InlineData(19045, false)]
    public void WindowsInfo_IsWindows11_MatchesBuild(int build, bool expected)
    {
        Assert.Equal(expected, new WindowsInfo { BuildNumber = build }.IsWindows11);
    }

    [Theory]
    [InlineData(10240, true)]
    [InlineData(19045, true)]
    [InlineData(22000, false)]
    public void WindowsInfo_IsWindows10_MatchesBuild(int build, bool expected)
    {
        Assert.Equal(expected, new WindowsInfo { BuildNumber = build }.IsWindows10);
    }

    [Fact]
    public void WindowsInfo_NullBuild_IsNeither11Nor10()
    {
        var info = new WindowsInfo();
        Assert.False(info.IsWindows11);
        Assert.False(info.IsWindows10);
    }

    [Theory]
    [InlineData("Professional", WindowsEdition.Pro)]
    [InlineData("Core", WindowsEdition.Home)]
    [InlineData("CoreSingleLanguage", WindowsEdition.Home)]
    [InlineData("Education", WindowsEdition.Education)]
    [InlineData("Enterprise", WindowsEdition.Enterprise)]
    [InlineData("ServerDatacenter", WindowsEdition.Server)]
    [InlineData(null, WindowsEdition.Unknown)]
    [InlineData("", WindowsEdition.Unknown)]
    [InlineData("Quantum", WindowsEdition.Unknown)]
    public void MapEdition_MapsKnownIds(string? editionId, WindowsEdition expected)
    {
        Assert.Equal(expected, WindowsProvider.MapEdition(editionId));
    }

    [Fact]
    public void PowerPlanService_UnknownScheme_ReadsNullName()
    {
        var service = new PowerPlanService(NullLogger<PowerPlanService>.Instance);
        // Random GUID: no friendly name exists, must be null (never guessed).
        Assert.Null(service.GetSchemeName(Guid.NewGuid()));
        Assert.False(service.SchemeExists(Guid.NewGuid()));
        Assert.Null(service.GetScheme(Guid.NewGuid()));
    }

    [Fact]
    public void PowerPlanService_BadDelete_FailsClosed()
    {
        var service = new PowerPlanService(NullLogger<PowerPlanService>.Instance);
        var call = new optimizerDuck.Domain.Execution.OpCall { Logger = NullLogger.Instance };
        // Deleting a nonexistent scheme must report failure, never throw.
        var result = service.DeleteScheme(call, Guid.NewGuid());
        Assert.False(result.Ok);
    }

    [Theory]
    [InlineData(0x13, MemoryType.Ddr2)]
    [InlineData(0x18, MemoryType.Ddr3)]
    [InlineData(0x1A, MemoryType.Ddr4)]
    [InlineData(0x22, MemoryType.Ddr5)]
    [InlineData(0x0F, MemoryType.Sdram)]
    [InlineData(0, MemoryType.Unknown)]
    [InlineData(null, MemoryType.Unknown)]
    [InlineData(0x99, MemoryType.Other)]
    public void MapMemoryType_MapsSmbiosCodes(int? code, MemoryType expected)
    {
        Assert.Equal(expected, MemoryProvider.MapMemoryType(code));
    }

    [Theory]
    [InlineData("GenuineIntel", CpuVendor.Intel)]
    [InlineData("AuthenticAMD", CpuVendor.Amd)]
    [InlineData(null, CpuVendor.Unknown)]
    [InlineData("", CpuVendor.Unknown)]
    [InlineData("QuantumCorp", CpuVendor.Unknown)]
    public void DetectCpuVendor_MatchesManufacturer(string? manufacturer, CpuVendor expected)
    {
        Assert.Equal(expected, CpuProvider.DetectVendor(manufacturer));
    }

    [Theory]
    [InlineData(0x10DE, GpuVendor.Nvidia)]
    [InlineData(0x1002, GpuVendor.Amd)]
    [InlineData(0x8086, GpuVendor.Intel)]
    [InlineData(0x1234, GpuVendor.Unknown)]
    public void DetectGpuVendorById_MatchesPciIds(uint vendorId, GpuVendor expected)
    {
        Assert.Equal(expected, GpuProvider.DetectVendorById(vendorId));
    }

    [Theory]
    [InlineData("NVIDIA GeForce RTX 4090", GpuVendor.Nvidia)]
    [InlineData("AMD Radeon RX 7900", GpuVendor.Amd)]
    [InlineData("Intel UHD Graphics 770", GpuVendor.Intel)]
    [InlineData("Quantum Render Device", GpuVendor.Unknown)]
    public void DetectGpuVendor_MatchesNames(string name, GpuVendor expected)
    {
        Assert.Equal(expected, GpuProvider.DetectVendor(name, null));
    }

    [Fact]
    public void GetPrimary_Empty_ReturnsNull()
    {
        Assert.Null(GpuProvider.GetPrimary([]));
    }

    [Fact]
    public void GetPrimary_PrefersMostVramThenVendor()
    {
        var intel = new GpuInfo
        {
            Name = "Intel UHD",
            Vendor = GpuVendor.Intel,
            VramMB = 128,
        };
        var amd = new GpuInfo
        {
            Name = "AMD Radeon",
            Vendor = GpuVendor.Amd,
            VramMB = 8192,
        };
        var nvidia = new GpuInfo
        {
            Name = "NVIDIA RTX",
            Vendor = GpuVendor.Nvidia,
            VramMB = 8192,
        };

        // Most VRAM wins regardless of order.
        Assert.Same(amd, GpuProvider.GetPrimary([intel, amd]));
        // VRAM tie breaks toward Nvidia.
        Assert.Same(nvidia, GpuProvider.GetPrimary([amd, nvidia]));
    }

    [Theory]
    [InlineData("Samsung SSD 980 PRO NVMe", StorageMediaType.Nvme)]
    [InlineData("WD Blue 3D NAND SSD", StorageMediaType.Ssd)]
    [InlineData("Seagate BarraCuda", StorageMediaType.Unknown)]
    public void InferFromModel_DoesNotGuess(string model, StorageMediaType expected)
    {
        Assert.Equal(expected, DiskProvider.InferFromModel(model));
    }

    [Fact]
    public void MaxSpeed_PicksHighestReported()
    {
        var modules = new List<MemoryModuleInfo>
        {
            new() { CapacityGB = 8, SpeedMTps = 3200 },
            new() { CapacityGB = 8 },
            new() { CapacityGB = 8, SpeedMTps = 3600 },
        };
        Assert.Equal(3600, MemoryProvider.MaxSpeed(modules));
        Assert.Null(MemoryProvider.MaxSpeed(new List<MemoryModuleInfo>()));
    }

    [Fact]
    public void DominantType_PicksMajorityIgnoresUnknown()
    {
        var modules = new List<MemoryModuleInfo>
        {
            new() { CapacityGB = 8, Type = MemoryType.Ddr4 },
            new() { CapacityGB = 8 },
            new() { CapacityGB = 8, Type = MemoryType.Ddr4 },
            new() { CapacityGB = 8, Type = MemoryType.Ddr5 },
        };
        Assert.Equal(MemoryType.Ddr4, MemoryProvider.DominantType(modules));
        Assert.Equal(MemoryType.Unknown, MemoryProvider.DominantType(new List<MemoryModuleInfo>()));
    }

    [Theory]
    [InlineData(new ushort[] { 9 }, DeviceKind.Laptop)]
    [InlineData(new ushort[] { 3 }, DeviceKind.Desktop)]
    [InlineData(new ushort[] { 99 }, DeviceKind.Unknown)]
    public void MapChassisType_MapsKnownTypes(ushort[] types, DeviceKind expected)
    {
        Assert.Equal(expected, WindowsProvider.MapChassisType(types));
    }

    [Theory]
    [InlineData(0, FirmwareMode.Uefi, VbsState.Disabled)]
    [InlineData(1, FirmwareMode.Uefi, VbsState.Enabled)]
    [InlineData(2, FirmwareMode.Uefi, VbsState.Enabled)]
    [InlineData(9, FirmwareMode.Uefi, VbsState.Unknown)]
    [InlineData(0, FirmwareMode.Legacy, VbsState.Unavailable)]
    [InlineData(2, FirmwareMode.Legacy, VbsState.Unavailable)]
    [InlineData(0, FirmwareMode.Unknown, VbsState.Disabled)]
    public void MapVbsStatus_MapsDeviceGuardStatus(int status, FirmwareMode mode, VbsState expected)
    {
        Assert.Equal(expected, SecurityProvider.MapVbsStatus(status, mode));
    }

    [Fact]
    public void MapMemoryIntegrity_RunningContainsHvciId()
    {
        Assert.Equal(VbsState.Enabled, SecurityProvider.MapMemoryIntegrity(new List<int> { 0, 2 }));
        Assert.Equal(VbsState.Disabled, SecurityProvider.MapMemoryIntegrity(new List<int> { 0 }));
        Assert.Equal(VbsState.Disabled, SecurityProvider.MapMemoryIntegrity(new List<int>()));
        Assert.Equal(
            VbsState.Disabled,
            SecurityProvider.MapMemoryIntegrity(new List<int> { 1, 3 })
        );
    }

    [Fact]
    public void ToServiceIds_HandlesWmiArrayShapes()
    {
        Assert.Equal(new List<int> { 0 }, SecurityProvider.ToServiceIds(new ushort[] { 0 }));
        Assert.Equal(new List<int> { 1, 2 }, SecurityProvider.ToServiceIds(new uint[] { 1, 2 }));
        Assert.Equal(new List<int>(), SecurityProvider.ToServiceIds(null));
        Assert.Equal(new List<int> { 2 }, SecurityProvider.ToServiceIds(2));
    }

    [Fact]
    public void SystemInfo_Unknown_IsMarked()
    {
        Assert.True(SystemInfo.Unknown.IsUnknown);
        var customized = SystemInfo.Unknown with { IsUnknown = false };
        Assert.False(customized.IsUnknown);
    }

    [Fact]
    public async Task RefreshAsync_CancelledToken_Throws()
    {
        var service = new SystemInfoService(NullLogger<SystemInfoService>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.RefreshAsync(cts.Token)
        );
    }

    [Fact]
    public async Task RefreshAsync_NotifiesAllHandlers_DespiteThrowingOne()
    {
        var service = new SystemInfoService(NullLogger<SystemInfoService>.Instance);
        var calls = 0;
        service.SnapshotRefreshed += (_, _) => calls++;
        service.SnapshotRefreshed += (_, _) => throw new InvalidOperationException("boom");
        service.SnapshotRefreshed += (_, _) => calls++;

        await service.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, calls);
    }
}
