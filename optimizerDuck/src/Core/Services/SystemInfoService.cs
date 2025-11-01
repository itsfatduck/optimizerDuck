using Microsoft.Extensions.Logging;
using optimizerDuck.UI;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Management;

namespace optimizerDuck.Core.Services;

// ============================================================================
// MODELS (Immutable Records)
// ============================================================================

public enum GpuVendor
{
    Unknown,
    NVIDIA,
    AMD,
    Intel
}

public sealed record GpuInfo(
    string Name,
    string DriverVersion,
    GpuVendor Vendor,
    int? MemoryMB,
    string? DeviceId,
    string? PnpDeviceId
)
{
    public static readonly GpuInfo Unknown = new("Unknown", "Unknown", GpuVendor.Unknown, null, null, null);

    public override string ToString() =>
        $"GPU: {Name}, Driver: {DriverVersion}, Vendor: {Vendor}, Memory: {MemoryMB} MB, DeviceId: {DeviceId}, PnpDeviceId: {PnpDeviceId}";
}

public sealed record CpuInfo(
    string Name,
    string Manufacturer,
    string Vendor,
    string Architecture,
    int Cores,
    int Threads,
    int MaxClockMHz,
    int CurrentClockMHz,
    int L2CacheKB,
    int L3CacheKB
)
{
    public static readonly CpuInfo Unknown = new("Unknown", "Unknown", "Unknown", "Unknown", 0, 0, 0, 0, 0, 0);
}

public sealed record RamModule(
    double CapacityGB,
    string SpeedMHz,
    string Manufacturer,
    string PartNumber,
    string DeviceLocator
);

public sealed record RamInfo(
    double TotalGB,
    long TotalMB,
    long TotalKB,
    double AvailableGB,
    double UsedPercent,
    IReadOnlyList<RamModule> Modules
)
{
    public static readonly RamInfo Unknown = new(0, 0, 0, 0, 0, []);
}

public sealed record OsInfo(
    string Name,
    string Version,
    string BuildNumber,
    string Edition,
    string Architecture,
    string DeviceType,
    string InstallDate,
    string LastBoot
)
{
    public static readonly OsInfo Unknown = new("Unknown", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown",
        "Unknown", "Unknown");
}

public sealed record BiosInfo(
    string Manufacturer,
    string Version,
    string ReleaseDate,
    string SmbiosVersion,
    string SerialNumber
)
{
    public static readonly BiosInfo Unknown = new("Unknown", "Unknown", "Unknown", "Unknown", "Unknown");
}

public sealed record SystemSnapshot(
    CpuInfo Cpu,
    RamInfo Ram,
    OsInfo Os,
    BiosInfo Bios,
    IReadOnlyList<GpuInfo> Gpus,
    GpuInfo? PrimaryGpu
)
{
    public static readonly SystemSnapshot Unknown = new(
        CpuInfo.Unknown,
        RamInfo.Unknown,
        OsInfo.Unknown,
        BiosInfo.Unknown,
        [],
        null
    );
}

// ============================================================================
// WMI HELPER
// ============================================================================

internal static class WmiHelper
{
    public static IEnumerable<ManagementObject> Query(string query)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            return searcher.Get().Cast<ManagementObject>().ToArray();
        }
        catch
        {
            return [];
        }
    }

    public static string GetString(ManagementObject mo, string property, string fallback = "Unknown")
    {
        try
        {
            var value = mo[property];
            return value switch
            {
                null => fallback,
                string[] arr => arr.Length > 0 ? arr[0].Trim() : fallback,
                _ => value.ToString()?.Trim() ?? fallback
            };
        }
        catch
        {
            return fallback;
        }
    }

    public static int GetInt(ManagementObject mo, string property, int fallback = 0)
    {
        try
        {
            var value = mo[property];
            return value == null ? fallback : Convert.ToInt32(value);
        }
        catch
        {
            return fallback;
        }
    }

    public static long GetLong(ManagementObject mo, string property, long fallback = 0)
    {
        try
        {
            var value = mo[property];
            return value == null ? fallback : Convert.ToInt64(value);
        }
        catch
        {
            return fallback;
        }
    }

    public static ManagementObject? GetFirst(string query)
    {
        return Query(query).FirstOrDefault();
    }
}

// ============================================================================
// COMPONENT PROVIDERS
// ============================================================================

internal static class CpuProvider
{
    public static CpuInfo Get()
    {
        try
        {
            var cpu = WmiHelper.GetFirst("SELECT * FROM Win32_Processor");
            if (cpu == null) return CpuInfo.Unknown;

            var name = WmiHelper.GetString(cpu, "Name");
            var manufacturer = WmiHelper.GetString(cpu, "Manufacturer");
            var cores = WmiHelper.GetInt(cpu, "NumberOfCores");
            var threads = WmiHelper.GetInt(cpu, "NumberOfLogicalProcessors");
            var maxMHz = WmiHelper.GetInt(cpu, "MaxClockSpeed");
            var curMHz = WmiHelper.GetInt(cpu, "CurrentClockSpeed");
            var l2kb = WmiHelper.GetInt(cpu, "L2CacheSize");
            var l3kb = WmiHelper.GetInt(cpu, "L3CacheSize");
            var addressWidth = WmiHelper.GetInt(cpu, "AddressWidth");

            var vendor = DetectCpuVendor(manufacturer);
            var architecture = addressWidth == 64 ? "64-bit" : addressWidth == 32 ? "32-bit" : "Unknown";

            return new CpuInfo(name, manufacturer, vendor, architecture, cores, threads, maxMHz, curMHz, l2kb, l3kb);
        }
        catch
        {
            return CpuInfo.Unknown;
        }
    }

    private static string DetectCpuVendor(string manufacturer)
    {
        var lower = manufacturer.ToLowerInvariant();
        if (lower.Contains("intel") || lower.Contains("genuineintel")) return "Intel";
        if (lower.Contains("amd") || lower.Contains("authenticamd")) return "AMD";
        return "Unknown";
    }
}

internal static class RamProvider
{
    public static RamInfo Get()
    {
        try
        {
            var modules = GetPhysicalModules();
            var (totalGB, totalMB, totalKB, availableGB, usedPercent) = GetMemoryStats(modules);

            return new RamInfo(totalGB, totalMB, totalKB, availableGB, usedPercent, modules);
        }
        catch
        {
            return RamInfo.Unknown;
        }
    }

    private static List<RamModule> GetPhysicalModules()
    {
        var modules = new List<RamModule>();
        var physicalMemory = WmiHelper.Query("SELECT * FROM Win32_PhysicalMemory");

        foreach (var mem in physicalMemory)
        {
            var capacityBytes = WmiHelper.GetLong(mem, "Capacity");
            var capacityGB = capacityBytes > 0 ? capacityBytes / Math.Pow(1024, 3) : 0;
            var speed = WmiHelper.GetString(mem, "Speed");
            var manufacturer = WmiHelper.GetString(mem, "Manufacturer");
            var partNumber = WmiHelper.GetString(mem, "PartNumber");
            var deviceLocator = WmiHelper.GetString(mem, "DeviceLocator");

            modules.Add(new RamModule(Math.Round(capacityGB, 2), speed, manufacturer, partNumber, deviceLocator));
        }

        return modules;
    }

    private static (double totalGB, long totalMB, long totalKB, double availableGB, double usedPercent) GetMemoryStats(
        List<RamModule> modules)
    {
        var totalGB = modules.Sum(m => m.CapacityGB);

        // Fallback to OS-reported total if modules don't provide accurate info
        if (totalGB <= 0)
        {
            var os = WmiHelper.GetFirst("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
            if (os != null)
            {
                var totalKBFromOs = WmiHelper.GetLong(os, "TotalVisibleMemorySize");
                totalGB = totalKBFromOs / 1024.0 / 1024.0;
            }
        }

        var totalMB = (long)(totalGB * 1024);
        var totalKB = totalMB * 1024;

        // Get available memory and usage
        var (availableGB, usedPercent) = GetMemoryUsage();

        return (Math.Round(totalGB, 2), totalMB, totalKB, availableGB, usedPercent);
    }

    private static (double availableGB, double usedPercent) GetMemoryUsage()
    {
        var os = WmiHelper.GetFirst("SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem");
        if (os == null) return (0, 0);

        var freeKB = WmiHelper.GetLong(os, "FreePhysicalMemory");
        var totalKB = WmiHelper.GetLong(os, "TotalVisibleMemorySize");

        if (totalKB == 0) return (0, 0);

        var availableGB = freeKB / 1024.0 / 1024.0;
        var usedKB = totalKB - freeKB;
        var usedPercent = Math.Round(usedKB * 100.0 / totalKB, 1);

        return (Math.Round(availableGB, 2), usedPercent);
    }
}

internal static class GpuProvider
{
    public static IReadOnlyList<GpuInfo> GetAll()
    {
        var gpus = new List<GpuInfo>();
        var controllers = WmiHelper.Query("SELECT * FROM Win32_VideoController");

        foreach (var controller in controllers)
        {
            var gpu = ParseGpu(controller);
            if (gpu != null && !IsVirtualAdapter(gpu.Name)) gpus.Add(gpu);
        }

        return gpus.Count > 0 ? gpus : [GpuInfo.Unknown];
    }

    private static GpuInfo? ParseGpu(ManagementObject controller)
    {
        var name = WmiHelper.GetString(controller, "Name", "");
        if (string.IsNullOrWhiteSpace(name)) return null;

        var driver = WmiHelper.GetString(controller, "DriverVersion");
        var deviceId = WmiHelper.GetString(controller, "DeviceID");
        var pnpId = WmiHelper.GetString(controller, "PNPDeviceID");
        var adapterRam = WmiHelper.GetLong(controller, "AdapterRAM");

        int? memoryMB = adapterRam > 0 ? (int)(adapterRam / (1024 * 1024)) : null;
        var vendor = DetectGpuVendor(name, pnpId);

        return new GpuInfo(name, driver, vendor, memoryMB, deviceId, pnpId);
    }

    private static bool IsVirtualAdapter(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.Contains("microsoft basic") ||
               lower.Contains("remote desktop") ||
               lower.Contains("virtual");
    }

    private static GpuVendor DetectGpuVendor(string name, string? pnpId)
    {
        var nameLower = name.ToLowerInvariant();
        var pnpLower = pnpId?.ToLowerInvariant() ?? "";

        if (nameLower.Contains("nvidia") || nameLower.Contains("geforce") ||
            nameLower.Contains("quadro") || nameLower.Contains("tesla") || pnpLower.Contains("ven_10de"))
            return GpuVendor.NVIDIA;

        if (nameLower.Contains("amd") || nameLower.Contains("radeon") ||
            nameLower.Contains("ryzen") || pnpLower.Contains("ven_1002"))
            return GpuVendor.AMD;

        if (nameLower.Contains("intel") || nameLower.Contains("iris") ||
            nameLower.Contains("uhd") || pnpLower.Contains("ven_8086"))
            return GpuVendor.Intel;

        return GpuVendor.Unknown;
    }

    public static GpuInfo? GetPrimary(IReadOnlyList<GpuInfo> gpus)
    {
        if (gpus == null || gpus.Count == 0) return null;

        return gpus
            .OrderByDescending(CalculateGpuScore)
            .FirstOrDefault();
    }

    private static int CalculateGpuScore(GpuInfo gpu)
    {
        var vendorScore = gpu.Vendor switch
        {
            GpuVendor.NVIDIA => 3,
            GpuVendor.AMD => 2,
            GpuVendor.Intel => 1,
            _ => 0
        };

        var memoryBonus = gpu.MemoryMB is > 1024 ? 10 : 0;

        return vendorScore + memoryBonus;
    }
}

internal static class OsProvider
{
    public static OsInfo Get()
    {
        try
        {
            var os = WmiHelper.GetFirst("SELECT * FROM Win32_OperatingSystem");
            if (os == null) return OsInfo.Unknown;

            var caption = WmiHelper.GetString(os, "Caption", "Windows");
            var buildNumber = WmiHelper.GetString(os, "BuildNumber");
            var version = MapWindowsVersion(WmiHelper.GetString(os, "Version"), buildNumber);
            var edition = ExtractEdition(caption);
            var architecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
            var deviceType = GetDeviceType();
            var installDate = FormatWmiDate(WmiHelper.GetString(os, "InstallDate", ""));
            var lastBoot = FormatWmiDateTime(WmiHelper.GetString(os, "LastBootUpTime", ""));
            var name = $"Windows {version}";

            return new OsInfo(name, version, buildNumber, edition, architecture, deviceType, installDate, lastBoot);
        }
        catch
        {
            return OsInfo.Unknown;
        }
    }

    private static string MapWindowsVersion(string version, string build)
    {
        if (version.StartsWith("6.1")) return "7";
        if (version.StartsWith("6.2")) return "8";
        if (version.StartsWith("6.3")) return "8.1";
        if (!version.StartsWith("10.0")) return "Unknown";

        return int.TryParse(build, out var buildNum) && buildNum >= 22000 ? "11" : "10";
    }

    private static string ExtractEdition(string caption)
    {
        if (string.IsNullOrWhiteSpace(caption)) return "Unknown";

        var parts = caption.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 2 ? parts.Last() : caption;
    }

    private static string GetDeviceType()
    {
        var chassis = WmiHelper.GetFirst("SELECT ChassisTypes FROM Win32_SystemEnclosure");
        if (chassis == null) return "Unknown";

        try
        {
            return chassis["ChassisTypes"] is ushort[] { Length: > 0 } types ? MapChassisType(types) : "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string MapChassisType(IEnumerable<ushort> types)
    {
        foreach (var type in types)
        {
            if (type is 8 or 9 or 10 or 11 or 14 or 30 or 31 or 32)
                return "Laptop";

            if (type is >= 1 and <= 7 or 12 or 13 or >= 15 and <= 29 or >= 33 and <= 36)
                return "Desktop";
        }

        return "Unknown";
    }

    private static string FormatWmiDate(string wmiDate)
    {
        if (wmiDate.Length < 8) return "Unknown";
        return $"{wmiDate[..4]}-{wmiDate.Substring(4, 2)}-{wmiDate.Substring(6, 2)}";
    }

    private static string FormatWmiDateTime(string wmiDate)
    {
        if (wmiDate.Length < 12) return "Unknown";
        return
            $"{wmiDate[..4]}-{wmiDate.Substring(4, 2)}-{wmiDate.Substring(6, 2)} {wmiDate.Substring(8, 2)}:{wmiDate.Substring(10, 2)}";
    }
}

internal static class BiosProvider
{
    public static BiosInfo Get()
    {
        try
        {
            var bios = WmiHelper.GetFirst("SELECT * FROM Win32_BIOS");
            if (bios == null) return BiosInfo.Unknown;

            var manufacturer = WmiHelper.GetString(bios, "Manufacturer");
            var version = WmiHelper.GetString(bios, "BIOSVersion");
            var smbiosVersion = WmiHelper.GetString(bios, "SMBIOSBIOSVersion");
            var serialNumber = WmiHelper.GetString(bios, "SerialNumber");
            var releaseDate = FormatWmiDate(WmiHelper.GetString(bios, "ReleaseDate", ""));

            return new BiosInfo(manufacturer, version, releaseDate, smbiosVersion, serialNumber);
        }
        catch
        {
            return BiosInfo.Unknown;
        }
    }

    private static string FormatWmiDate(string wmiDate)
    {
        if (wmiDate.Length < 8) return "Unknown";
        return $"{wmiDate[..4]}-{wmiDate.Substring(4, 2)}-{wmiDate.Substring(6, 2)}";
    }
}

// ============================================================================
// MAIN SERVICE (Public API)
// ============================================================================

public static class SystemInfoService
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public static SystemSnapshot Snapshot { get; private set; } = SystemSnapshot.Unknown;

    public static async Task<SystemSnapshot> RefreshAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var tasks = new Task[]
            {
                Task.Run(CpuProvider.Get, ct),
                Task.Run(RamProvider.Get, ct),
                Task.Run(OsProvider.Get, ct),
                Task.Run(BiosProvider.Get, ct),
                Task.Run(GpuProvider.GetAll, ct)
            };

            await Task.WhenAll(tasks).ConfigureAwait(false);

            var cpu = ((Task<CpuInfo>)tasks[0]).Result;
            var ram = ((Task<RamInfo>)tasks[1]).Result;
            var os = ((Task<OsInfo>)tasks[2]).Result;
            var bios = ((Task<BiosInfo>)tasks[3]).Result;
            var gpus = ((Task<IReadOnlyList<GpuInfo>>)tasks[4]).Result;
            var primaryGpu = GpuProvider.GetPrimary(gpus);

            var snapshot = new SystemSnapshot(cpu, ram, os, bios, gpus, primaryGpu);
            Snapshot = snapshot;

            return snapshot;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public static void GetSummary(SystemSnapshot s, ILogger log)
    {
        log.LogDebug("OS : {OsName} {OsEdition} {OsArchitecture} ({OsDeviceType})",
            s.Os.Name, s.Os.Edition, s.Os.Architecture, s.Os.DeviceType);

        LogGpuSummary(s, log);

        log.LogDebug("CPU: {CpuName} ({CpuVendor}) [{CpuCores} Cores/{CpuThreads} Threads]",
            s.Cpu.Name, s.Cpu.Vendor, s.Cpu.Cores, s.Cpu.Threads);

        log.LogDebug("RAM: {RamTotalGb:F1} GB (Used: {RamUsedPercent:F1}%)",
            s.Ram.TotalGB, s.Ram.UsedPercent);
    }

    private static void LogGpuSummary(SystemSnapshot s, ILogger log)
    {
        var gpuCount = s.Gpus.Count;

        if (gpuCount == 0 || (gpuCount == 1 && s.Gpus[0] == GpuInfo.Unknown))
        {
            log.LogDebug("GPU: None detected");
            return;
        }

        if (gpuCount == 1)
        {
            var gpu = s.Gpus[0];
            var memoryInfo = gpu.MemoryMB.HasValue ? $" [{gpu.MemoryMB.Value / 1024.0:F0} GB]" : "";
            log.LogDebug("GPU: {GpuName} ({GpuVendor}){MemoryInfo}", gpu.Name, gpu.Vendor, memoryInfo);
            return;
        }

        // Multiple GPUs
        if (s.PrimaryGpu != null)
        {
            var memoryInfo = s.PrimaryGpu.MemoryMB.HasValue
                ? $" [{s.PrimaryGpu.MemoryMB.Value / 1024.0:F0} GB]"
                : "";
            log.LogDebug("GPU (Primary): {PrimaryGpuName} ({PrimaryGpuVendor}){MemoryInfo}",
                s.PrimaryGpu.Name, s.PrimaryGpu.Vendor, memoryInfo);
        }

        log.LogDebug("Total GPUs : {GpuCount}", gpuCount);
    }

    public static Panel GetDetailedPanel(SystemSnapshot s)
    {
        // Operating System Information
        var osGroup = new List<IRenderable>
        {
            new Rule("Operating System Information") { Style = Theme.Primary },
            new Markup($"[{Theme.Info}]Name           :[/] [bold]{s.Os.Name}[/]"),
            new Markup($"[{Theme.Info}]Version        :[/] [{Theme.Success}]{s.Os.Version}[/]"),
            new Markup($"[{Theme.Info}]Build Number   :[/] [{Theme.Success}]{s.Os.BuildNumber}[/]"),
            new Markup($"[{Theme.Info}]Edition        :[/] [{Theme.Warning}]{s.Os.Edition}[/]"),
            new Markup($"[{Theme.Info}]Architecture   :[/] [bold]{s.Os.Architecture}[/]"),
            new Markup($"[{Theme.Info}]Device Type    :[/] [bold]{s.Os.DeviceType}[/]"),
            new Markup($"[{Theme.Info}]Install Date   :[/] [{Theme.Success}]{s.Os.InstallDate}[/]"),
            new Markup($"[{Theme.Info}]Last Boot      :[/] [{Theme.Success}]{s.Os.LastBoot}[/]")
        };

        // BIOS Information
        var biosGroup = new List<IRenderable>
        {
            new Rule("BIOS Information") { Style = Theme.Success },
            new Markup($"[{Theme.Info}]Manufacturer   :[/] [bold]{s.Bios.Manufacturer}[/]"),
            new Markup($"[{Theme.Info}]Version        :[/] [{Theme.Success}]{s.Bios.Version}[/]"),
            new Markup($"[{Theme.Info}]Release Date   :[/] [{Theme.Success}]{s.Bios.ReleaseDate}[/]"),
            new Markup($"[{Theme.Info}]SMBIOS Version :[/] [{Theme.Success}]{s.Bios.SmbiosVersion}[/]")
        };

        // CPU Information
        var cpuVendorColor = s.Cpu.Vendor == "AMD" ? Theme.Error : s.Cpu.Vendor == "Intel" ? Theme.Info : Theme.Muted;
        var cpuGroup = new List<IRenderable>
        {
            new Rule("CPU Information") { Style = Theme.Info },
            new Markup($"[{Theme.Info}]Name           :[/] [bold]{s.Cpu.Name}[/]"),
            new Markup($"[{Theme.Info}]Vendor         :[/] [{cpuVendorColor}]{s.Cpu.Vendor}[/]"),
            new Markup($"[{Theme.Info}]Manufacturer   :[/] [bold]{s.Cpu.Manufacturer}[/]"),
            new Markup($"[{Theme.Info}]Cores          :[/] [{Theme.Success}]{s.Cpu.Cores}[/]"),
            new Markup($"[{Theme.Info}]Threads        :[/] [{Theme.Success}]{s.Cpu.Threads}[/]"),
            new Markup($"[{Theme.Info}]Architecture   :[/] [bold]{s.Cpu.Architecture}[/]"),
            new Markup($"[{Theme.Info}]Max Clock Speed:[/] [{Theme.Warning}]{s.Cpu.MaxClockMHz}[/] [dim]MHz[/]"),
            new Markup($"[{Theme.Info}]Current Speed  :[/] [{Theme.Warning}]{s.Cpu.CurrentClockMHz}[/] [dim]MHz[/]"),
            new Markup($"[{Theme.Info}]L2 Cache       :[/] [{Theme.Success}]{s.Cpu.L2CacheKB}[/] [dim]KB[/]"),
            new Markup($"[{Theme.Info}]L3 Cache       :[/] [{Theme.Success}]{s.Cpu.L3CacheKB}[/] [dim]KB[/]")
        };

        // RAM Information
        var usedColor = s.Ram.UsedPercent > 80 ? Theme.Error : s.Ram.UsedPercent > 60 ? Theme.Warning : Theme.Success;
        var ramGroup = new List<IRenderable>
        {
            new Rule("Memory Information") { Style = new Style(Color.Yellow) },
            new Markup(
                $"[{Theme.Info}]Total          :[/] [{Theme.Success}]{s.Ram.TotalGB:F2}[/] [dim]GB[/] [dim]([/][blue]{s.Ram.TotalMB}[/] [dim]MB)[/]"),
            new Markup($"[{Theme.Info}]Available      :[/] [{Theme.Success}]{s.Ram.AvailableGB:F2}[/] [dim]GB[/]"),
            new Markup($"[{Theme.Info}]Used           :[/] [{usedColor}]{s.Ram.UsedPercent:F1}%[/]")
        };

        if (s.Ram.Modules.Count > 0)
        {
            ramGroup.Add(new Markup(
                $"[{Theme.Info}]Modules        :[/] [{Theme.Success}]{s.Ram.Modules.Count}[/] [dim]detected[/]"));
            for (var i = 0; i < s.Ram.Modules.Count; i++)
            {
                var module = s.Ram.Modules[i];
                ramGroup.Add(new Markup(
                    $"  [{Theme.Info}]Module {i + 1}     :[/] [{Theme.Warning}]{module.CapacityGB:F2}[/] [dim]GB @[/] [{Theme.Success}]{module.SpeedMHz}[/] [dim]MHz[/]"));
                ramGroup.Add(new Markup($"    [dim]Manufacturer:[/] [{Theme.Info}]{module.Manufacturer}[/]"));
                ramGroup.Add(new Markup($"    [dim]Part Number :[/] [bold]{module.PartNumber}[/]"));
                ramGroup.Add(new Markup($"    [dim]Location    :[/] [bold]{module.DeviceLocator}[/]"));
            }
        }

        // GPU Information
        var gpuCount = s.Gpus.Count;
        var gpuGroup = new List<IRenderable>();

        if (gpuCount == 0 || (gpuCount == 1 && s.Gpus[0] == GpuInfo.Unknown))
        {
            gpuGroup.Add(new Rule("GPU Information") { Style = new Style(Color.Red) });
            gpuGroup.Add(new Markup($"[{Theme.Error}]No GPUs detected[/]"));
        }
        else if (gpuCount == 1)
        {
            gpuGroup.Add(new Rule("GPU Information") { Style = new Style(Color.Green) });
            var gpu = s.Gpus[0];
            var vendorColor = gpu.Vendor switch
            {
                GpuVendor.NVIDIA => Theme.Success,
                GpuVendor.AMD => Theme.Error,
                GpuVendor.Intel => Theme.Accent,
                _ => Theme.Muted
            };

            gpuGroup.Add(new Markup($"[{Theme.Info}]Name           :[/] [bold]{gpu.Name}[/]"));
            gpuGroup.Add(new Markup($"[{Theme.Info}]Vendor         :[/] [{vendorColor}]{gpu.Vendor}[/]"));
            gpuGroup.Add(new Markup($"[{Theme.Info}]Driver Version :[/] [{Theme.Warning}]{gpu.DriverVersion}[/]"));

            if (gpu.MemoryMB.HasValue)
                gpuGroup.Add(new Markup(
                    $"[{Theme.Info}]Memory         :[/] [{Theme.Success}]{gpu.MemoryMB.Value}[/] [dim]MB[/] [dim]([/][{Theme.Success}]{gpu.MemoryMB.Value / 1024.0:F2}[/] [dim]GB)[/]"));

            if (!string.IsNullOrEmpty(gpu.DeviceId))
                gpuGroup.Add(new Markup($"[{Theme.Info}]Device ID      :[/] [dim]{gpu.DeviceId}[/]"));

            if (!string.IsNullOrEmpty(gpu.PnpDeviceId))
                gpuGroup.Add(new Markup($"[{Theme.Info}]PCI Device ID  :[/] [dim]{gpu.PnpDeviceId}[/]"));
        }
        else
        {
            gpuGroup.Add(new Rule($"GPU Information ({gpuCount} detected)") { Style = new Style(Color.Green) });
            for (var i = 0; i < s.Gpus.Count; i++)
            {
                var gpu = s.Gpus[i];
                var isPrimary = s.PrimaryGpu != null && gpu.Name == s.PrimaryGpu.Name ? " (Primary)" : "";
                var vendorColor = gpu.Vendor switch
                {
                    GpuVendor.NVIDIA => Theme.Success,
                    GpuVendor.AMD => Theme.Error,
                    GpuVendor.Intel => Theme.Accent,
                    _ => Theme.Muted
                };

                gpuGroup.Add(new Markup($"[{Theme.Info}]GPU {i + 1}{isPrimary}:[/]"));
                gpuGroup.Add(new Markup($"  [{Theme.Info}]Name         :[/] [bold]{gpu.Name}[/]"));
                gpuGroup.Add(
                    new Markup($"  [{Theme.Info}]Vendor       :[/] [{vendorColor}]{gpu.Vendor}[/{vendorColor}]"));
                gpuGroup.Add(new Markup($"  [{Theme.Info}]Driver       :[/] [{Theme.Warning}]{gpu.DriverVersion}[/]"));

                if (gpu.MemoryMB.HasValue)
                    gpuGroup.Add(new Markup(
                        $"  [{Theme.Info}]Memory       :[/] [{Theme.Success}]{gpu.MemoryMB.Value}[/] [dim]MB[/] [dim]([/][{Theme.Success}]{gpu.MemoryMB.Value / 1024.0:F2}[/] [dim]GB)[/]"));

                if (!string.IsNullOrEmpty(gpu.DeviceId))
                    gpuGroup.Add(new Markup($"  [{Theme.Info}]Device ID    :[/] [dim]{gpu.DeviceId}[/]"));

                if (!string.IsNullOrEmpty(gpu.PnpDeviceId))
                    gpuGroup.Add(new Markup($"  [{Theme.Info}]PCI Device ID:[/] [dim]{gpu.PnpDeviceId}[/]"));
            }
        }

        // Combine all groups
        var allGroups = new List<IRenderable>();
        allGroups.AddRange(osGroup);
        allGroups.Add(Text.Empty);
        allGroups.AddRange(biosGroup);
        allGroups.Add(Text.Empty);
        allGroups.AddRange(cpuGroup);
        allGroups.Add(Text.Empty);
        allGroups.AddRange(ramGroup);
        allGroups.Add(Text.Empty);
        allGroups.AddRange(gpuGroup);

        var panel = new Panel(new Rows(allGroups))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Theme.PrimaryMuted,
            Width = 90
        };

        return panel;
    }
}