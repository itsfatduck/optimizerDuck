using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using optimizerDuck.Resources.Languages;

namespace optimizerDuck.Services;

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
    public static readonly GpuInfo Unknown = new(Translations.Common_Unknown, Translations.Common_Unknown,
        GpuVendor.Unknown, null, null, null);

    public override string ToString()
    {
        return
            $"GPU: {Name}, Driver: {DriverVersion}, Vendor: {Vendor}, Memory: {MemoryMB} MB, DeviceId: {DeviceId}, PnpDeviceId: {PnpDeviceId}";
    }
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
    public static readonly CpuInfo Unknown = new(Translations.Common_Unknown, Translations.Common_Unknown,
        Translations.Common_Unknown, Translations.Common_Unknown, 0, 0, 0, 0, 0, 0);
}

public sealed record DiskVolume(
    string Letter,
    bool SystemDrive,
    string FileSystem,
    string DriveType,
    string Label,
    double TotalSizeGB,
    double FreeSpaceGB,
    double UsedSpaceGB,
    double UsedPercent,
    string MediaType, // SSD, HDD, or Unknown
    bool IsRemovable,
    string? SerialNumber,
    string? Model
);

public sealed record DiskInfo(
    IReadOnlyList<DiskVolume> Volumes
)
{
    public static readonly DiskInfo Unknown = new([]);
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
    double UsedGB,
    IReadOnlyList<RamModule> Modules
)
{
    public static readonly RamInfo Unknown = new(0, 0, 0, 0, 0, 0, []);
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
    public static readonly OsInfo Unknown = new(Translations.Common_Unknown, Translations.Common_Unknown,
        Translations.Common_Unknown, Translations.Common_Unknown, Translations.Common_Unknown,
        Translations.Common_Unknown,
        Translations.Common_Unknown, Translations.Common_Unknown);
}

public sealed record BiosInfo(
    string Manufacturer,
    string Version,
    string ReleaseDate,
    string SmbiosVersion,
    string SerialNumber
)
{
    public static readonly BiosInfo Unknown = new(Translations.Common_Unknown, Translations.Common_Unknown,
        Translations.Common_Unknown, Translations.Common_Unknown, Translations.Common_Unknown);
}

public sealed record SystemSnapshot(
    CpuInfo Cpu,
    RamInfo Ram,
    OsInfo Os,
    BiosInfo Bios,
    IReadOnlyList<GpuInfo> Gpus,
    GpuInfo? PrimaryGpu,
    DiskInfo Disk
)
{
    public static readonly SystemSnapshot Unknown = new(
        CpuInfo.Unknown,
        RamInfo.Unknown,
        OsInfo.Unknown,
        BiosInfo.Unknown,
        [],
        null,
        DiskInfo.Unknown
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

    public static string GetString(ManagementObject mo, string property, string? fallback = null)
    {
        fallback ??= Translations.Common_Unknown;
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
            var (totalGB, totalMB, totalKB, availableGB, usedPercent, usedGB) = GetMemoryStats(modules);

            return new RamInfo(totalGB, totalMB, totalKB, availableGB, usedPercent, usedGB, modules);
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

    private static (double totalGB, long totalMB, long totalKB, double availableGB, double usedPercent, double usedGB)
        GetMemoryStats(
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
        var (availableGB, usedGB, usedPercent) = GetMemoryUsage();

        return (Math.Round(totalGB, 2), totalMB, totalKB, availableGB, usedPercent, usedGB);
    }

    private static (double availableGB, double usedGB, double usedPercent) GetMemoryUsage()
    {
        var os = WmiHelper.GetFirst("SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem");
        if (os == null) return (0, 0, 0);

        var freeKB = WmiHelper.GetLong(os, "FreePhysicalMemory");
        var totalKB = WmiHelper.GetLong(os, "TotalVisibleMemorySize");

        if (totalKB == 0) return (0, 0, 0);

        var availableGB = freeKB / 1024.0 / 1024.0;
        var usedKB = totalKB - freeKB;
        var usedPercent = Math.Round(usedKB * 100.0 / totalKB, 1);

        return (Math.Round(availableGB, 2), Math.Round(usedKB / 1024.0 / 1024.0, 2), usedPercent);
    }
}

public static class DiskHelper
{
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    // Using 0 (FILE_READ_ATTRIBUTES or NULL access) allows us to query
    // device metadata without requiring Administrator privileges.
    // i know admin rights is required by default when open app but this just a fallback at least
    private const uint GENERIC_READ = 0x0; // No access requested; allows querying info without Admin rights

    private const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x2D1400;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode,
        IntPtr lpInBuffer, uint nInBufferSize,
        IntPtr lpOutBuffer, uint nOutBufferSize,
        out uint lpBytesReturned, IntPtr lpOverlapped);

    public static string DetectMediaType(string driveLetter)
    {
        // Try multiple detection methods
        var seekPenalty = DetectViaSSDSeekPenalty(driveLetter);
        if (seekPenalty != "Unknown")
            return seekPenalty;

        var wmiType = DetectViaWMI(driveLetter);
        if (wmiType != "Unknown")
            return wmiType;

        return "Unknown";
    }

    private static string DetectViaSSDSeekPenalty(string driveLetter)
    {
        try
        {
            var trimmedDrive = driveLetter.TrimEnd('\\', '/');
            if (string.IsNullOrWhiteSpace(trimmedDrive))
                return "Unknown";

            using var handle = CreateFile(@"\\.\" + trimmedDrive, GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero,
                OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, IntPtr.Zero);

            if (handle.IsInvalid)
                return "Unknown";

            var query = new STORAGE_PROPERTY_QUERY
            {
                PropertyId = STORAGE_PROPERTY_ID.StorageDeviceSeekPenaltyProperty,
                QueryType = STORAGE_QUERY_TYPE.PropertyStandardQuery,
                AdditionalParameters = new byte[1]
            };

            var querySize = Marshal.SizeOf(query);
            var resultSize = Marshal.SizeOf<DEVICE_SEEK_PENALTY_DESCRIPTOR>();

            var queryPtr = IntPtr.Zero;
            var resultPtr = IntPtr.Zero;

            try
            {
                queryPtr = Marshal.AllocHGlobal(querySize);
                Marshal.StructureToPtr(query, queryPtr, false);

                resultPtr = Marshal.AllocHGlobal(resultSize);

                var success = DeviceIoControl(handle, IOCTL_STORAGE_QUERY_PROPERTY,
                    queryPtr, (uint)querySize,
                    resultPtr, (uint)resultSize,
                    out _, IntPtr.Zero);

                if (!success)
                    return "Unknown";

                var descriptor = Marshal.PtrToStructure<DEVICE_SEEK_PENALTY_DESCRIPTOR>(resultPtr);
                return descriptor.IncursSeekPenalty ? "HDD" : "SSD";
            }
            finally
            {
                if (queryPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(queryPtr);

                if (resultPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(resultPtr);
            }
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string DetectViaWMI(string driveLetter)
    {
        try
        {
            // Get physical disk number from logical drive
            var partitions = WmiHelper.Query(
                $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetter.TrimEnd('\\')}' }} WHERE AssocClass=Win32_LogicalDiskToPartition");

            foreach (var partition in partitions)
            {
                var diskDrives = WmiHelper.Query(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");

                foreach (var disk in diskDrives)
                {
                    var mediaType = WmiHelper.GetString(disk, "MediaType", "").ToLowerInvariant();
                    var model = WmiHelper.GetString(disk, "Model", "").ToLowerInvariant();

                    // Check for SSD indicators
                    if (mediaType.Contains("ssd") || model.Contains("ssd") ||
                        model.Contains("nvme") || mediaType.Contains("fixed hard disk media"))
                    {
                        // Additional check for rotational rate
                        var capabilities = disk["Capabilities"];
                        if (capabilities != null && capabilities is ushort[] caps)
                            // Check if non-rotational (3 = Rotational, 4 = SSD)
                            if (caps.Contains((ushort)4))
                                return "SSD";

                        // Fallback to model name check
                        if (model.Contains("ssd") || model.Contains("nvme"))
                            return "SSD";
                    }

                    if (mediaType.Contains("removable"))
                        return "Unknown"; // Could be USB drive
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return "Unknown";
    }

    public static bool IsSystemDrive(string driveLetter)
    {
        var systemDrive = Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\') ?? "C:";
        return string.Equals(systemDrive, driveLetter.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
    }

    private enum STORAGE_PROPERTY_ID
    {
        StorageDeviceSeekPenaltyProperty = 7
    }

    private enum STORAGE_QUERY_TYPE
    {
        PropertyStandardQuery = 0
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STORAGE_PROPERTY_QUERY
    {
        public STORAGE_PROPERTY_ID PropertyId;
        public STORAGE_QUERY_TYPE QueryType;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public byte[] AdditionalParameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVICE_SEEK_PENALTY_DESCRIPTOR
    {
        public uint Version;
        public uint Size;
        [MarshalAs(UnmanagedType.U1)] public bool IncursSeekPenalty;
    }
}

internal static class DiskProvider
{
    public static DiskInfo Get()
    {
        try
        {
            static double ToGb(long bytes)
            {
                return Math.Round(bytes / Math.Pow(1024, 3), 2);
            }

            // Get physical disk information
            var physicalDisks = GetPhysicalDiskInfo();

            var volumes = DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(drive =>
                {
                    var totalBytes = drive.TotalSize;
                    var freeBytes = drive.AvailableFreeSpace;
                    var usedBytes = totalBytes - freeBytes;

                    var isSystemDrive = DiskHelper.IsSystemDrive(drive.Name);
                    var mediaType = DiskHelper.DetectMediaType(drive.Name);
                    var isRemovable = drive.DriveType == DriveType.Removable;

                    // Try to get disk info for this drive
                    var diskInfo = GetDiskInfoForDrive(drive.Name, physicalDisks);

                    return new DiskVolume(
                        drive.Name.TrimEnd('\\'),
                        isSystemDrive,
                        drive.DriveFormat,
                        drive.DriveType.ToString(),
                        string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel,
                        ToGb(totalBytes),
                        ToGb(freeBytes),
                        ToGb(usedBytes),
                        totalBytes > 0 ? Math.Round(usedBytes * 100.0 / totalBytes, 1) : 0,
                        mediaType,
                        isRemovable,
                        diskInfo?.SerialNumber,
                        diskInfo?.Model
                    );
                })
                .ToList();

            return new DiskInfo(volumes);
        }
        catch
        {
            return DiskInfo.Unknown;
        }
    }

    private static List<PhysicalDiskInfo> GetPhysicalDiskInfo()
    {
        var disks = new List<PhysicalDiskInfo>();
        try
        {
            var diskDrives = WmiHelper.Query("SELECT * FROM Win32_DiskDrive");
            foreach (var disk in diskDrives)
                disks.Add(new PhysicalDiskInfo
                {
                    DeviceID = WmiHelper.GetString(disk, "DeviceID"),
                    Model = WmiHelper.GetString(disk, "Model"),
                    SerialNumber = WmiHelper.GetString(disk, "SerialNumber").Trim(),
                    MediaType = WmiHelper.GetString(disk, "MediaType")
                });
        }
        catch
        {
            // Ignore errors
        }

        return disks;
    }

    private static PhysicalDiskInfo? GetDiskInfoForDrive(string driveLetter, List<PhysicalDiskInfo> physicalDisks)
    {
        try
        {
            var letter = driveLetter.TrimEnd('\\');
            var partitions = WmiHelper.Query(
                $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{letter}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");

            foreach (var partition in partitions)
            {
                var diskDrives = WmiHelper.Query(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");

                foreach (var disk in diskDrives)
                {
                    var deviceId = WmiHelper.GetString(disk, "DeviceID");
                    return physicalDisks.FirstOrDefault(p => p.DeviceID == deviceId);
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }

    private class PhysicalDiskInfo
    {
        public string DeviceID { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty;
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
        if (gpus.Count == 0) return null;

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
            switch (type)
            {
                case 8 or 9 or 10 or 11 or 14 or 30 or 31 or 32:
                    return "Laptop";

                case >= 1 and <= 7 or 12 or 13 or >= 15 and <= 29 or >= 33 and <= 36:
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

public sealed class SystemInfoService
{
    private readonly ILogger<SystemInfoService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private BiosInfo? _cachedBios;

    // Cache CPU, OS, BIOS info (doesn't change at runtime)
    private CpuInfo? _cachedCpu;
    private OsInfo? _cachedOs;

    public SystemInfoService(ILogger<SystemInfoService> logger)
    {
        _logger = logger;
    }

    public SystemSnapshot Snapshot { get; private set; } = SystemSnapshot.Unknown;

    public async Task<SystemSnapshot> RefreshAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var (ram, gpus, disk) = await Task.Run(async () =>
            {
                var ramTask = Task.Run(RamProvider.Get, ct);
                var gpusTask = Task.Run(GpuProvider.GetAll, ct);
                var diskTask = Task.Run(DiskProvider.Get, ct);

                await Task.WhenAll(ramTask, gpusTask, diskTask);

                return (await ramTask, await gpusTask, await diskTask);
            }, ct);

            _cachedCpu ??= await Task.Run(CpuProvider.Get, ct);
            _cachedOs ??= await Task.Run(OsProvider.Get, ct);
            _cachedBios ??= await Task.Run(BiosProvider.Get, ct);

            var primaryGpu = GpuProvider.GetPrimary(gpus);

            var snapshot = new SystemSnapshot(_cachedCpu, ram, _cachedOs, _cachedBios, gpus, primaryGpu, disk);
            Snapshot = snapshot;

            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh system info");
            return SystemSnapshot.Unknown;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void LogSummary()
    {
        try
        {
            _logger.LogInformation("OS : {OsName} {OsEdition} [{OsArchitecture}] ({OsDeviceType})",
                Snapshot.Os.Name, Snapshot.Os.Edition, Snapshot.Os.Architecture, Snapshot.Os.DeviceType);

            LogGpuSummary();

            _logger.LogInformation("CPU: {CpuName} [{CpuVendor}] ({CpuCores} Cores/{CpuThreads} Threads)",
                Snapshot.Cpu.Name, Snapshot.Cpu.Vendor, Snapshot.Cpu.Cores, Snapshot.Cpu.Threads);

            _logger.LogInformation("RAM: {RamTotalGb:F1} GB [{RamModules} Module(s)] (Used: {RamUsedGB:F1} GB)",
                Snapshot.Ram.TotalGB, Snapshot.Ram.Modules.Count, Snapshot.Ram.UsedGB);

            foreach (var volume in Snapshot.Disk.Volumes)
            {
                var systemDrive = volume.SystemDrive ? " [System Drive]" : "";
                var modelInfo = !string.IsNullOrEmpty(volume.Model) ? $" - {volume.Model}" : "";
                _logger.LogInformation(
                    "Disk {VolumeLetter}{SystemDrive} [{MediaType}] {VolumeTotalSizeGb:F1} GB (Free: {VolumeFreeSpaceGb:F1} GB){ModelInfo}",
                    volume.Letter, systemDrive, volume.MediaType, volume.TotalSizeGB, volume.FreeSpaceGB, modelInfo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log system summary");
        }
    }

    private void LogGpuSummary()
    {
        var gpuCount = Snapshot.Gpus.Count;

        if (gpuCount == 0 || (gpuCount == 1 && Snapshot.Gpus[0] == GpuInfo.Unknown))
        {
            _logger.LogWarning("GPU: None detected");
            return;
        }

        if (gpuCount == 1)
        {
            var gpu = Snapshot.Gpus[0];
            var memoryInfo = gpu.MemoryMB.HasValue ? $" ({gpu.MemoryMB.Value / 1024.0:F0} GB)" : "";
            _logger.LogInformation("GPU: {GpuName} [{GpuVendor}]{MemoryInfo}", gpu.Name, gpu.Vendor, memoryInfo);
            return;
        }

        // Multiple GPUs
        if (Snapshot.PrimaryGpu != null)
        {
            var memoryInfoPrimary = Snapshot.PrimaryGpu.MemoryMB.HasValue
                ? $" ({Snapshot.PrimaryGpu.MemoryMB.Value / 1024.0:F0} GB)"
                : "";
            _logger.LogInformation("GPU (Primary): {PrimaryGpuName} [{PrimaryGpuVendor}]{MemoryInfo}",
                Snapshot.PrimaryGpu.Name, Snapshot.PrimaryGpu.Vendor, memoryInfoPrimary);

            for (var index = 0; index < Snapshot.Gpus.Count; index++)
            {
                if (Snapshot.Gpus[index].Name == Snapshot.PrimaryGpu.Name
                    && Snapshot.Gpus[index].Vendor == Snapshot.PrimaryGpu.Vendor)
                    continue;

                var gpu = Snapshot.Gpus[index];
                var memoryInfo = gpu.MemoryMB.HasValue
                    ? $" [{gpu.MemoryMB.Value / 1024.0:F0} GB]"
                    : "";
                _logger.LogInformation("GPU ({GpuIndex}): {GpuName} ({GpuVendor}){MemoryInfo}",
                    index + 1, gpu.Name, gpu.Vendor, memoryInfo);
            }

            _logger.LogInformation("Total GPUs : {GpuCount}", gpuCount);
        }
    }
}