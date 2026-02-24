using System.Globalization;
using System.IO;
using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
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
// WMI HELPER (with connection caching)
// ============================================================================

internal static class WmiHelper
{
    private static readonly Dictionary<string, ManagementScope> ScopeCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock ScopeLock = new();

    /// <summary>
    ///     Gets or creates a cached ManagementScope for the given namespace.
    /// </summary>
    private static ManagementScope GetScope(string namespacePath = @"root\cimv2")
    {
        lock (ScopeLock)
        {
            if (ScopeCache.TryGetValue(namespacePath, out var cached) && cached.IsConnected)
                return cached;

            var scope = new ManagementScope(namespacePath);
            scope.Connect();
            ScopeCache[namespacePath] = scope;
            return scope;
        }
    }

    public static IEnumerable<ManagementObject> Query(string query, string namespacePath = @"root\cimv2")
    {
        try
        {
            var scope = GetScope(namespacePath);
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query));
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

    public static ManagementObject? GetFirst(string query, string namespacePath = @"root\cimv2")
    {
        return Query(query, namespacePath).FirstOrDefault();
    }
}

// ============================================================================
// NATIVE MEMORY API (Task Manager accuracy)
// ============================================================================

internal static class NativeMemory
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    /// <summary>
    ///     Returns current memory status using the same API Task Manager uses.
    /// </summary>
    public static MEMORYSTATUSEX? GetMemoryStatus()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        return GlobalMemoryStatusEx(ref status) ? status : null;
    }
}

// ============================================================================
// COMPONENT PROVIDERS
// ============================================================================

internal static class CpuProvider
{
    /// <summary>
    ///     Gets CPU info using Registry (fast) + targeted WMI (only for cache sizes).
    ///     Registry path: HKLM\HARDWARE\DESCRIPTION\System\CentralProcessor\0
    /// </summary>
    public static CpuInfo Get()
    {
        try
        {
            // ── Fast path: Registry ──────────────────────────────────────
            var name = Translations.Common_Unknown;
            var manufacturer = Translations.Common_Unknown;
            var currentMHz = 0;

            using (var cpuKey = Registry.LocalMachine.OpenSubKey(
                       @"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
            {
                if (cpuKey != null)
                {
                    name = cpuKey.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? name;
                    manufacturer = cpuKey.GetValue("VendorIdentifier")?.ToString()?.Trim() ?? manufacturer;
                    currentMHz = cpuKey.GetValue("~MHz") is int mhz ? mhz : 0;
                }
            }

            // Thread count via Environment (instant, no WMI)
            var threads = Environment.ProcessorCount;

            // ── Targeted WMI: only fields not available in Registry ──────
            var cores = 0;
            var maxMHz = currentMHz; // fallback to registry MHz
            var l2kb = 0;
            var l3kb = 0;

            var cpu = WmiHelper.GetFirst(
                "SELECT NumberOfCores, MaxClockSpeed, L2CacheSize, L3CacheSize FROM Win32_Processor");
            if (cpu != null)
            {
                cores = WmiHelper.GetInt(cpu, "NumberOfCores");
                maxMHz = WmiHelper.GetInt(cpu, "MaxClockSpeed", maxMHz);
                l2kb = WmiHelper.GetInt(cpu, "L2CacheSize");
                l3kb = WmiHelper.GetInt(cpu, "L3CacheSize");
            }

            if (cores == 0) cores = threads; // fallback

            var vendor = DetectCpuVendor(manufacturer);
            var architecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";

            return new CpuInfo(name, manufacturer, vendor, architecture, cores, threads, maxMHz, currentMHz, l2kb,
                l3kb);
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
    /// <summary>
    ///     Gets RAM info using GlobalMemoryStatusEx (same API as Task Manager)
    ///     for usage stats, and WMI only for physical module details.
    /// </summary>
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
        var physicalMemory =
            WmiHelper.Query("SELECT Capacity, Speed, Manufacturer, PartNumber, DeviceLocator FROM Win32_PhysicalMemory");

        foreach (var mem in physicalMemory)
        {
            var capacityBytes = WmiHelper.GetLong(mem, "Capacity");
            var capacityGB = capacityBytes > 0 ? capacityBytes / (1024.0 * 1024.0 * 1024.0) : 0;
            var speed = WmiHelper.GetString(mem, "Speed");
            var manufacturer = WmiHelper.GetString(mem, "Manufacturer");
            var partNumber = WmiHelper.GetString(mem, "PartNumber");
            var deviceLocator = WmiHelper.GetString(mem, "DeviceLocator");

            modules.Add(new RamModule(Math.Round(capacityGB, 2), speed, manufacturer, partNumber, deviceLocator));
        }

        return modules;
    }

    private static (double totalGB, long totalMB, long totalKB, double availableGB, double usedPercent, double usedGB)
        GetMemoryStats(List<RamModule> modules)
    {
        // ── Primary: GlobalMemoryStatusEx (Task Manager accuracy) ────────
        var memStatus = NativeMemory.GetMemoryStatus();
        if (memStatus.HasValue)
        {
            var status = memStatus.Value;

            var totalBytes = (double)status.ullTotalPhys;
            var availBytes = (double)status.ullAvailPhys;
            var usedBytes = totalBytes - availBytes;

            var totalGB = Math.Round(totalBytes / (1024.0 * 1024.0 * 1024.0), 2);
            var totalMB = (long)(totalBytes / (1024.0 * 1024.0));
            var totalKB = (long)(totalBytes / 1024.0);
            var availableGB = Math.Round(availBytes / (1024.0 * 1024.0 * 1024.0), 2);
            var usedGB = Math.Round(usedBytes / (1024.0 * 1024.0 * 1024.0), 2);
            var usedPercent = (double)status.dwMemoryLoad; // Already calculated by Windows

            return (totalGB, totalMB, totalKB, availableGB, usedPercent, usedGB);
        }

        // ── Fallback: module-based calculation ───────────────────────────
        var fallbackTotalGB = modules.Sum(m => m.CapacityGB);
        if (fallbackTotalGB <= 0) fallbackTotalGB = 0;

        var fallbackTotalMB = (long)(fallbackTotalGB * 1024);
        var fallbackTotalKB = fallbackTotalMB * 1024;

        return (Math.Round(fallbackTotalGB, 2), fallbackTotalMB, fallbackTotalKB, 0, 0, 0);
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
    /// <summary>
    ///     Gets disk info using MSFT_PhysicalDisk (modern Storage namespace) for
    ///     accurate media type detection, with Win32_DiskDrive as fallback.
    /// </summary>
    public static DiskInfo Get()
    {
        try
        {
            static double ToGb(long bytes)
            {
                return Math.Round(bytes / (1024.0 * 1024.0 * 1024.0), 2);
            }

            // Build physical disk info using modern MSFT_PhysicalDisk (preferred)
            // then fallback to Win32_DiskDrive if needed
            var physicalDisks = GetPhysicalDiskInfo();
            var diskNumberToInfo = BuildDiskNumberMap(physicalDisks);

            var volumes = DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(drive =>
                {
                    var totalBytes = drive.TotalSize;
                    var freeBytes = drive.AvailableFreeSpace;
                    var usedBytes = totalBytes - freeBytes;

                    var isSystemDrive = DiskHelper.IsSystemDrive(drive.Name);
                    var isRemovable = drive.DriveType == System.IO.DriveType.Removable;

                    // Try to get disk info for this drive (model, serial, media type)
                    var diskInfo = GetDiskInfoForDrive(drive.Name, diskNumberToInfo);

                    // Media type priority: PhysicalDisk info > seek penalty P/Invoke > WMI fallback
                    var mediaType = diskInfo?.MediaType ?? DiskHelper.DetectMediaType(drive.Name);

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

    /// <summary>
    ///     Gets physical disk info, preferring MSFT_PhysicalDisk (accurate SSD/HDD)
    ///     with Win32_DiskDrive as fallback.
    /// </summary>
    private static List<PhysicalDiskInfo> GetPhysicalDiskInfo()
    {
        var disks = GetViaMsftPhysicalDisk();
        if (disks.Count > 0) return disks;

        // Fallback to Win32_DiskDrive
        return GetViaWin32DiskDrive();
    }

    /// <summary>
    ///     Uses MSFT_PhysicalDisk from the modern Storage namespace.
    ///     MediaType: 3 = HDD, 4 = SSD (matches Task Manager's disk type display).
    /// </summary>
    private static List<PhysicalDiskInfo> GetViaMsftPhysicalDisk()
    {
        var disks = new List<PhysicalDiskInfo>();
        try
        {
            var physicalDisks = WmiHelper.Query(
                "SELECT DeviceId, FriendlyName, SerialNumber, MediaType, BusType FROM MSFT_PhysicalDisk",
                @"root\Microsoft\Windows\Storage");

            foreach (var disk in physicalDisks)
            {
                var mediaTypeValue = WmiHelper.GetInt(disk, "MediaType");
                var mediaType = mediaTypeValue switch
                {
                    3 => "HDD",
                    4 => "SSD",
                    5 => "SCM", // Storage Class Memory
                    _ => "Unknown"
                };

                disks.Add(new PhysicalDiskInfo
                {
                    DiskNumber = WmiHelper.GetString(disk, "DeviceId", "-1"),
                    Model = WmiHelper.GetString(disk, "FriendlyName"),
                    SerialNumber = WmiHelper.GetString(disk, "SerialNumber").Trim(),
                    MediaType = mediaType,
                    DeviceID = "" // Not available from MSFT_PhysicalDisk
                });
            }
        }
        catch
        {
            // Storage namespace not available (older OS)
        }

        return disks;
    }

    /// <summary>
    ///     Fallback: classic Win32_DiskDrive WMI provider.
    /// </summary>
    private static List<PhysicalDiskInfo> GetViaWin32DiskDrive()
    {
        var disks = new List<PhysicalDiskInfo>();
        try
        {
            var diskDrives = WmiHelper.Query("SELECT DeviceID, Model, SerialNumber, MediaType, Index FROM Win32_DiskDrive");
            foreach (var disk in diskDrives)
                disks.Add(new PhysicalDiskInfo
                {
                    DeviceID = WmiHelper.GetString(disk, "DeviceID"),
                    DiskNumber = WmiHelper.GetInt(disk, "Index").ToString(),
                    Model = WmiHelper.GetString(disk, "Model"),
                    SerialNumber = WmiHelper.GetString(disk, "SerialNumber").Trim(),
                    MediaType = "" // Win32_DiskDrive MediaType is unreliable
                });
        }
        catch
        {
            // Ignore errors
        }

        return disks;
    }

    /// <summary>
    ///     Builds a lookup from disk number to PhysicalDiskInfo.
    /// </summary>
    private static Dictionary<int, PhysicalDiskInfo> BuildDiskNumberMap(List<PhysicalDiskInfo> disks)
    {
        var map = new Dictionary<int, PhysicalDiskInfo>();
        foreach (var disk in disks)
        {
            if (int.TryParse(disk.DiskNumber, out var num))
                map.TryAdd(num, disk);
        }

        return map;
    }

    /// <summary>
    ///     Maps a logical drive letter to a physical disk using Win32 associations.
    /// </summary>
    private static PhysicalDiskInfo? GetDiskInfoForDrive(string driveLetter,
        Dictionary<int, PhysicalDiskInfo> diskNumberMap)
    {
        if (diskNumberMap.Count == 0) return null;

        try
        {
            var letter = driveLetter.TrimEnd('\\');

            // Try MSFT_Partition to get DiskNumber for this drive letter
            var partitions = WmiHelper.Query(
                $"SELECT DiskNumber FROM MSFT_Partition WHERE DriveLetter = '{letter.TrimEnd(':')}'",
                @"root\Microsoft\Windows\Storage");

            foreach (var partition in partitions)
            {
                var diskNum = WmiHelper.GetInt(partition, "DiskNumber", -1);
                if (diskNum >= 0 && diskNumberMap.TryGetValue(diskNum, out var info))
                    return info;
            }

            // Fallback: classic WMI ASSOCIATORS path
            var classicPartitions = WmiHelper.Query(
                $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{letter}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");

            foreach (var partition in classicPartitions)
            {
                var diskDrives = WmiHelper.Query(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");

                foreach (var disk in diskDrives)
                {
                    var index = WmiHelper.GetInt(disk, "Index", -1);
                    if (index >= 0 && diskNumberMap.TryGetValue(index, out var info))
                        return info;

                    // Direct match by DeviceID
                    var deviceId = WmiHelper.GetString(disk, "DeviceID");
                    foreach (var entry in diskNumberMap.Values)
                    {
                        if (entry.DeviceID == deviceId)
                            return entry;
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        // If only one physical disk, assume it's the one
        if (diskNumberMap.Count == 1)
            return diskNumberMap.Values.First();

        return null;
    }

    private class PhysicalDiskInfo
    {
        public string DeviceID { get; set; } = string.Empty;
        public string DiskNumber { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty;
    }
}

// ============================================================================
// DXGI INTEROP (COM P/Invoke)
// ============================================================================

internal static class DxgiHelper
{
    private static readonly Guid IID_IDXGIFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");

    [DllImport("dxgi.dll", PreserveSig = false)]
    private static extern void CreateDXGIFactory1(
        [In] ref Guid riid,
        [Out, MarshalAs(UnmanagedType.Interface)] out IDXGIFactory1 factory);

    public static IDXGIFactory1 CreateFactory()
    {
        var iid = IID_IDXGIFactory1;
        CreateDXGIFactory1(ref iid, out var factory);
        return factory;
    }

    [ComImport]
    [Guid("770aae78-f26f-4dba-a829-253c83d1b387")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDXGIFactory1
    {
        // IDXGIObject (4 methods)
        void SetPrivateData([In] ref Guid Name, uint DataSize, IntPtr pData);
        void SetPrivateDataInterface([In] ref Guid Name, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
        void GetPrivateData([In] ref Guid Name, ref uint pDataSize, IntPtr pData);
        void GetParent([In] ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppParent);

        // IDXGIFactory (4 methods)
        void EnumAdapters(uint Adapter, [MarshalAs(UnmanagedType.IUnknown)] out object ppAdapter);
        void MakeWindowAssociation(IntPtr WindowHandle, uint Flags);
        void GetWindowAssociation(out IntPtr pWindowHandle);
        void CreateSwapChain(
            [MarshalAs(UnmanagedType.IUnknown)] object pDevice,
            IntPtr pDesc,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppSwapChain);
        void CreateSoftwareAdapter(IntPtr Module, [MarshalAs(UnmanagedType.IUnknown)] out object ppAdapter);

        // IDXGIFactory1 (2 methods)
        [PreserveSig]
        int EnumAdapters1(uint Adapter, out IDXGIAdapter1? ppAdapter);
        bool IsCurrent();
    }

    [ComImport]
    [Guid("29038f61-3839-4626-91fd-086879011a05")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDXGIAdapter1
    {
        // IDXGIObject (4 methods)
        void SetPrivateData([In] ref Guid Name, uint DataSize, IntPtr pData);
        void SetPrivateDataInterface([In] ref Guid Name, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
        void GetPrivateData([In] ref Guid Name, ref uint pDataSize, IntPtr pData);
        void GetParent([In] ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppParent);

        // IDXGIAdapter (3 methods)
        void EnumOutputs(uint Output, [MarshalAs(UnmanagedType.IUnknown)] out object ppOutput);
        void GetDesc(out DXGI_ADAPTER_DESC pDesc);
        int CheckInterfaceSupport([In] ref Guid InterfaceName, out long pUMDVersion);

        // IDXGIAdapter1 (1 method)
        void GetDesc1(out DXGI_ADAPTER_DESC1 pDesc);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DXGI_ADAPTER_DESC
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public nuint DedicatedVideoMemory;
        public nuint DedicatedSystemMemory;
        public nuint SharedSystemMemory;
        public long AdapterLuid;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DXGI_ADAPTER_DESC1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public nuint DedicatedVideoMemory;
        public nuint DedicatedSystemMemory;
        public nuint SharedSystemMemory;
        public long AdapterLuid;
        public uint Flags; // DXGI_ADAPTER_FLAG
    }

    // DXGI_ADAPTER_FLAG values
    public const uint DXGI_ADAPTER_FLAG_SOFTWARE = 2;

    // Microsoft Basic Render Driver identifiers
    private const uint MICROSOFT_VENDOR_ID = 0x1414;
    private const uint BASIC_RENDER_DEVICE_ID = 0x8C;

    public static bool IsSoftwareAdapter(DXGI_ADAPTER_DESC1 desc)
    {
        if ((desc.Flags & DXGI_ADAPTER_FLAG_SOFTWARE) != 0)
            return true;

        // Microsoft Basic Render Driver
        return desc.VendorId == MICROSOFT_VENDOR_ID && desc.DeviceId == BASIC_RENDER_DEVICE_ID;
    }
}

// ============================================================================
// GPU PROVIDER (DXGI + minimal WMI)
// ============================================================================

internal static class GpuProvider
{
    /// <summary>
    ///     Enumerates all physical GPUs using DXGI, with WMI fallback for driver info.
    ///     The returned list is ordered by DXGI adapter index (index 0 = primary display adapter).
    /// </summary>
    public static IReadOnlyList<GpuInfo> GetAll()
    {
        try
        {
            return GetAllViaDxgi();
        }
        catch
        {
            // DXGI unavailable (e.g., ancient OS or headless server) — fall back to WMI
            return GetAllViaWmi();
        }
    }

    /// <summary>
    ///     Selects the primary performance GPU from the list.
    ///     On hybrid laptops DXGI index 0 is often the iGPU (Intel), so we
    ///     pick by highest VRAM first, then vendor priority (NVIDIA > AMD > Intel).
    ///     Intel is only considered primary when it is the sole GPU.
    /// </summary>
    public static GpuInfo? GetPrimary(IReadOnlyList<GpuInfo> gpus)
    {
        if (gpus.Count == 0) return null;
        if (gpus.Count == 1) return gpus[0];

        return gpus
            .OrderByDescending(g => g.MemoryMB ?? 0)
            .ThenByDescending(g => g.Vendor == GpuVendor.NVIDIA)
            .ThenByDescending(g => g.Vendor == GpuVendor.AMD)
            .First();
    }

    // ── DXGI path ──────────────────────────────────────────────────────────

    private static IReadOnlyList<GpuInfo> GetAllViaDxgi()
    {
        var factory = DxgiHelper.CreateFactory();
        var wmiLookup = BuildWmiLookup(); // lightweight WMI for DriverVersion + DeviceID
        var gpus = new List<GpuInfo>();

        try
        {
            for (uint i = 0; ; i++)
            {
                var hr = factory.EnumAdapters1(i, out var adapter);
                if (hr != 0 || adapter == null) break; // DXGI_ERROR_NOT_FOUND

                try
                {
                    adapter.GetDesc1(out var desc);

                    // Skip software / virtual adapters
                    if (DxgiHelper.IsSoftwareAdapter(desc))
                        continue;

                    var name = desc.Description?.Trim() ?? "";
                    if (string.IsNullOrWhiteSpace(name) || IsVirtualAdapter(name))
                        continue;

                    var vendor = DetectGpuVendorById(desc.VendorId);
                    var memoryMB = (int)((long)desc.DedicatedVideoMemory / (1024 * 1024));

                    // Match to WMI entry for DriverVersion and DeviceID
                    var wmiMatch = FindWmiMatch(name, desc.VendorId, desc.DeviceId, wmiLookup);

                    gpus.Add(new GpuInfo(
                        name,
                        wmiMatch?.DriverVersion ?? Translations.Common_Unknown,
                        vendor,
                        memoryMB > 0 ? memoryMB : null,
                        wmiMatch?.DeviceId,
                        wmiMatch?.PnpDeviceId
                    ));
                }
                finally
                {
                    Marshal.ReleaseComObject(adapter);
                }
            }
        }
        finally
        {
            if (factory != null) Marshal.ReleaseComObject(factory);
        }

        return gpus.Count > 0 ? gpus : [GpuInfo.Unknown];
    }

    // ── WMI fallback path ──────────────────────────────────────────────────

    private static IReadOnlyList<GpuInfo> GetAllViaWmi()
    {
        var gpus = new List<GpuInfo>();
        var controllers = WmiHelper.Query("SELECT Name, DriverVersion, DeviceID, PNPDeviceID, AdapterRAM FROM Win32_VideoController");

        foreach (var controller in controllers)
        {
            var name = WmiHelper.GetString(controller, "Name", "");
            if (string.IsNullOrWhiteSpace(name) || IsVirtualAdapter(name))
                continue;

            var driver = WmiHelper.GetString(controller, "DriverVersion");
            var deviceId = WmiHelper.GetString(controller, "DeviceID");
            var pnpId = WmiHelper.GetString(controller, "PNPDeviceID");
            var adapterRam = WmiHelper.GetLong(controller, "AdapterRAM");

            int? memoryMB = adapterRam > 0 ? (int)(adapterRam / (1024 * 1024)) : null;
            var vendor = DetectGpuVendor(name, pnpId);

            gpus.Add(new GpuInfo(name, driver, vendor, memoryMB, deviceId, pnpId));
        }

        return gpus.Count > 0 ? gpus : [GpuInfo.Unknown];
    }

    // ── WMI lookup for DXGI matching ───────────────────────────────────────

    private sealed record WmiGpuEntry(
        string Name,
        string DriverVersion,
        string? DeviceId,
        string? PnpDeviceId,
        uint VendorId,
        uint HardwareDeviceId
    );

    /// <summary>
    ///     Builds a lightweight WMI lookup table (Name, DriverVersion, DeviceID, PNPDeviceID)
    ///     for cross-referencing with DXGI adapters.
    /// </summary>
    private static List<WmiGpuEntry> BuildWmiLookup()
    {
        var entries = new List<WmiGpuEntry>();
        try
        {
            var controllers = WmiHelper.Query(
                "SELECT Name, DriverVersion, DeviceID, PNPDeviceID FROM Win32_VideoController");

            foreach (var controller in controllers)
            {
                var name = WmiHelper.GetString(controller, "Name", "");
                var driver = WmiHelper.GetString(controller, "DriverVersion");
                var deviceId = WmiHelper.GetString(controller, "DeviceID");
                var pnpId = WmiHelper.GetString(controller, "PNPDeviceID");

                // Extract VendorId and DeviceId from PNP DeviceID (e.g., PCI\VEN_10DE&DEV_2684&...)
                ParsePnpIds(pnpId, out var vendorId, out var hardwareDeviceId);

                entries.Add(new WmiGpuEntry(name, driver, deviceId, pnpId, vendorId, hardwareDeviceId));
            }
        }
        catch
        {
            // WMI unavailable — driver info won't be available
        }

        return entries;
    }

    /// <summary>
    ///     Parses PNP DeviceID string like "PCI\VEN_10DE&amp;DEV_2684&amp;SUBSYS_..." to extract VendorId and DeviceId.
    /// </summary>
    private static void ParsePnpIds(string? pnpId, out uint vendorId, out uint hardwareDeviceId)
    {
        vendorId = 0;
        hardwareDeviceId = 0;
        if (string.IsNullOrWhiteSpace(pnpId)) return;

        var upper = pnpId.ToUpperInvariant();

        var venIdx = upper.IndexOf("VEN_", StringComparison.Ordinal);
        if (venIdx >= 0 && venIdx + 8 <= upper.Length)
        {
            if (uint.TryParse(upper.Substring(venIdx + 4, 4),
                    NumberStyles.HexNumber, null, out var v))
                vendorId = v;
        }

        var devIdx = upper.IndexOf("DEV_", StringComparison.Ordinal);
        if (devIdx >= 0 && devIdx + 8 <= upper.Length)
        {
            if (uint.TryParse(upper.Substring(devIdx + 4, 4),
                    NumberStyles.HexNumber, null, out var d))
                hardwareDeviceId = d;
        }
    }

    /// <summary>
    ///     Matches a DXGI adapter to its WMI entry.
    ///     Priority: VendorId+DeviceId match > name substring match.
    /// </summary>
    private static WmiGpuEntry? FindWmiMatch(
        string dxgiName, uint dxgiVendorId, uint dxgiDeviceId, List<WmiGpuEntry> wmiEntries)
    {
        // First pass: match by hardware IDs (most reliable)
        foreach (var entry in wmiEntries)
        {
            if (entry.VendorId == dxgiVendorId && entry.HardwareDeviceId == dxgiDeviceId)
                return entry;
        }

        // Second pass: match by name substring
        var dxgiNameLower = dxgiName.ToLowerInvariant();
        foreach (var entry in wmiEntries)
        {
            if (!string.IsNullOrEmpty(entry.Name) &&
                dxgiNameLower.Contains(entry.Name.ToLowerInvariant()))
                return entry;
        }

        return null;
    }

    // ── Shared helpers ─────────────────────────────────────────────────────

    private static bool IsVirtualAdapter(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.Contains("microsoft basic") ||
               lower.Contains("remote desktop") ||
               lower.Contains("virtual");
    }

    /// <summary>
    ///     Detects GPU vendor from DXGI VendorId (PCI Vendor ID).
    /// </summary>
    private static GpuVendor DetectGpuVendorById(uint vendorId)
    {
        return vendorId switch
        {
            0x10DE => GpuVendor.NVIDIA,
            0x1002 => GpuVendor.AMD,
            0x8086 => GpuVendor.Intel,
            _ => GpuVendor.Unknown
        };
    }

    /// <summary>
    ///     Detects GPU vendor from name and PNP DeviceID strings (WMI fallback path).
    /// </summary>
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
}

internal static class OsProvider
{
    /// <summary>
    ///     Gets OS info using Registry (fast) + targeted WMI (only for LastBootUpTime).
    /// </summary>
    public static OsInfo Get()
    {
        try
        {
            // ── Fast path: Registry ──────────────────────────────────────
            var buildNumber = Translations.Common_Unknown;
            var edition = Translations.Common_Unknown;
            var installDate = Translations.Common_Unknown;
            var displayVersion = "";

            using (var ntKey = Registry.LocalMachine.OpenSubKey(
                       @"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
            {
                if (ntKey != null)
                {
                    buildNumber = ntKey.GetValue("CurrentBuildNumber")?.ToString()
                                  ?? ntKey.GetValue("CurrentBuild")?.ToString()
                                  ?? buildNumber;
                    edition = ntKey.GetValue("EditionID")?.ToString() ?? edition;
                    displayVersion = ntKey.GetValue("DisplayVersion")?.ToString() ?? "";

                    // Install date from Registry (Unix timestamp)
                    if (ntKey.GetValue("InstallDate") is int installTimestamp && installTimestamp > 0)
                    {
                        var installDateTime = DateTimeOffset.FromUnixTimeSeconds(installTimestamp);
                        installDate = installDateTime.LocalDateTime.ToString("yyyy-MM-dd");
                    }
                }
            }

            // Determine Windows version from build number
            var version = MapWindowsVersion(buildNumber);
            var architecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
            var deviceType = GetDeviceType();
            var name = $"Windows {version}";

            // ── Targeted WMI: only for LastBootUpTime ────────────────────
            var lastBoot = Translations.Common_Unknown;
            var os = WmiHelper.GetFirst("SELECT LastBootUpTime FROM Win32_OperatingSystem");
            if (os != null)
                lastBoot = FormatWmiDateTime(WmiHelper.GetString(os, "LastBootUpTime", ""));

            return new OsInfo(name, version, buildNumber, edition, architecture, deviceType, installDate, lastBoot);
        }
        catch
        {
            return OsInfo.Unknown;
        }
    }

    private static string MapWindowsVersion(string build)
    {
        if (!int.TryParse(build, out var buildNum))
            return "Unknown";

        // Rough mapping based on build number ranges
        return buildNum switch
        {
            >= 22000 => "11",
            >= 10240 => "10",
            >= 9600 => "8.1",
            >= 9200 => "8",
            >= 7600 => "7",
            _ => "Unknown"
        };
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
                    return Translations.Dashboard_SystemInfo_Os_DeviceType_Laptop;

                case >= 1 and <= 7 or 12 or 13 or >= 15 and <= 29 or >= 33 and <= 36:
                    return Translations.Dashboard_SystemInfo_Os_DeviceType_Desktop;
            }

        return Translations.Common_Unknown;
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
            var bios = WmiHelper.GetFirst("SELECT Manufacturer, BIOSVersion, SMBIOSBIOSVersion, SerialNumber, ReleaseDate FROM Win32_BIOS");
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
    private IReadOnlyList<GpuInfo>? _cachedGpus;
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
            var isFirstRun = _cachedCpu == null;

            if (isFirstRun)
            {
                // First run: fetch everything in parallel
                var cpuTask = Task.Run(CpuProvider.Get, ct);
                var ramTask = Task.Run(RamProvider.Get, ct);
                var gpusTask = Task.Run(GpuProvider.GetAll, ct);
                var diskTask = Task.Run(DiskProvider.Get, ct);
                var osTask = Task.Run(OsProvider.Get, ct);
                var biosTask = Task.Run(BiosProvider.Get, ct);

                await Task.WhenAll(cpuTask, ramTask, gpusTask, diskTask, osTask, biosTask);

                _cachedCpu = await cpuTask;
                _cachedOs = await osTask;
                _cachedBios = await biosTask;
                _cachedGpus = await gpusTask;

                var primaryGpu = GpuProvider.GetPrimary(_cachedGpus);

                Snapshot = new SystemSnapshot(_cachedCpu, await ramTask, _cachedOs, _cachedBios, _cachedGpus,
                    primaryGpu, await diskTask);
            }
            else
            {
                // Subsequent runs: only refresh dynamic data (RAM, Disk)
                // GPU list changes very rarely, so cache it too
                var ramTask = Task.Run(RamProvider.Get, ct);
                var diskTask = Task.Run(DiskProvider.Get, ct);

                await Task.WhenAll(ramTask, diskTask);

                var gpus = _cachedGpus!;
                var primaryGpu = GpuProvider.GetPrimary(gpus);

                Snapshot = new SystemSnapshot(_cachedCpu!, await ramTask, _cachedOs!, _cachedBios!, gpus, primaryGpu,
                    await diskTask);
            }

            return Snapshot;
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