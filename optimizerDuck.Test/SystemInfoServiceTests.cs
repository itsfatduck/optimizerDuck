using Spectre.Console;
using optimizerDuck.Core.Services;

namespace optimizerDuck.Test.Core.Services;

public class SystemInfoServiceTests
{
    [Fact]
    public void GetDetailedPanel_WithValidSnapshot_ReturnsPanel()
    {
        // Arrange
        var snapshot = CreateValidSystemSnapshot();

        // Act
        var panel = SystemInfoService.GetDetailedPanel(snapshot);

        // Assert
        Assert.NotNull(panel);
        Assert.IsType<Panel>(panel);
        Assert.Equal(90, panel.Width);
        Assert.Equal(BoxBorder.Rounded, panel.Border);
    }

    [Fact]
    public void GetDetailedPanel_WithSingleGpu_RendersGpuInformation()
    {
        // Arrange
        var gpu = new GpuInfo("NVIDIA RTX 3080", "537.13", GpuVendor.NVIDIA, 10240, "PCI\\VEN_10DE", "PCI_VEN_10DE");
        var snapshot = CreateValidSystemSnapshot() with { Gpus = [gpu], PrimaryGpu = gpu };

        // Act
        var panel = SystemInfoService.GetDetailedPanel(snapshot);

        // Assert
        Assert.NotNull(panel);
    }

    [Fact]
    public void GetDetailedPanel_WithMultipleGpus_RendersAllGpus()
    {
        // Arrange
        var gpu1 = new GpuInfo("NVIDIA RTX 3080", "537.13", GpuVendor.NVIDIA, 10240, "PCI\\VEN_10DE", "PCI_VEN_10DE");
        var gpu2 = new GpuInfo("Intel UHD Graphics", "27.20", GpuVendor.Intel, 1536, "PCI\\VEN_8086", "PCI_VEN_8086");
        var snapshot = CreateValidSystemSnapshot() with { Gpus = [gpu1, gpu2], PrimaryGpu = gpu1 };

        // Act
        var panel = SystemInfoService.GetDetailedPanel(snapshot);

        // Assert
        Assert.NotNull(panel);
    }

    [Fact]
    public void GetDetailedPanel_WithNoGpus_HandlesGracefully()
    {
        // Arrange
        var snapshot = CreateValidSystemSnapshot() with { Gpus = [], PrimaryGpu = null };

        // Act
        var panel = SystemInfoService.GetDetailedPanel(snapshot);

        // Assert
        Assert.NotNull(panel);
    }

    [Fact]
    public void GetDetailedPanel_WithUnknownGpu_HandlesGracefully()
    {
        // Arrange
        var snapshot = CreateValidSystemSnapshot() with { Gpus = [GpuInfo.Unknown], PrimaryGpu = null };

        // Act
        var panel = SystemInfoService.GetDetailedPanel(snapshot);

        // Assert
        Assert.NotNull(panel);
    }

    [Fact]
    public void GetDetailedPanel_WithHighMemoryUsage_ReturnsPanel()
    {
        // Arrange
        var ram = new RamInfo(32, 32768, 33554432, 5.5, 85.0, []);
        var snapshot = CreateValidSystemSnapshot() with { Ram = ram };

        // Act
        var panel = SystemInfoService.GetDetailedPanel(snapshot);

        // Assert
        Assert.NotNull(panel);
    }

    [Fact]
    public void GetDetailedPanel_WithRamModules_RendersModuleDetails()
    {
        // Arrange
        var module1 = new RamModule(16.0, "3200", "Corsair", "CMK16GX4M2B3200C16", "DIMM 1");
        var module2 = new RamModule(16.0, "3200", "Kingston", "KF432C16BB2K2/32", "DIMM 2");
        var ram = new RamInfo(32, 32768, 33554432, 16.0, 50.0, [module1, module2]);
        var snapshot = CreateValidSystemSnapshot() with { Ram = ram };

        // Act
        var panel = SystemInfoService.GetDetailedPanel(snapshot);

        // Assert
        Assert.NotNull(panel);
    }

    private static SystemSnapshot CreateValidSystemSnapshot()
    {
        var cpu = new CpuInfo(
            "Intel(R) Core(TM) i9-12900K",
            "GenuineIntel",
            "Intel",
            "64-bit",
            16,
            24,
            5200,
            3200,
            8192,
            30720
        );

        var ram = new RamInfo(32, 32768, 33554432, 20.0, 50.0, []);

        var os = new OsInfo(
            "Windows 11",
            "11",
            "22621",
            "Pro",
            "64-bit",
            "Desktop",
            "2022-01-15",
            "2025-11-03 10:30"
        );

        var bios = new BiosInfo(
            "American Megatrends Inc.",
            "F14",
            "2022-01-10",
            "3.4",
            "ABCDEF123456"
        );

        var gpu = new GpuInfo(
            "NVIDIA GeForce RTX 3080",
            "537.13",
            GpuVendor.NVIDIA,
            10240,
            "PCI\\VEN_10DE",
            "PCI_VEN_10DE"
        );

        return new SystemSnapshot(cpu, ram, os, bios, [gpu], gpu);
    }
}