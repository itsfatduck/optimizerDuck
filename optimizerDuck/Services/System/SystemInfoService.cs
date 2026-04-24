using System.Globalization;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using optimizerDuck.Resources.Languages;

namespace optimizerDuck.Services;

// ============================================================================
// MODELS (Immutable Records)
// ============================================================================

/// <summary>
///     Specifies the GPU vendor manufacturer.
/// </summary>
public enum GpuVendor
{
    /// <summary>Unknown or unrecognized GPU vendor.</summary>
    Unknown,

    /// <summary>NVIDIA Corporation.</summary>
    NVIDIA,

    /// <summary>Advanced Micro Devices (AMD).</summary>
    AMD,

    /// <summary>Intel Corporation.</summary>
    Intel,
}

/// <summary>
///     Represents information about a GPU.
/// </summary>
public sealed record GpuInfo
{
    /// <summary>
    ///     A sentinel value representing an unknown GPU.
    /// </summary>
    public static readonly GpuInfo Unknown = new()
    {
        Name = Translations.Common_Unknown,
        DriverVersion = Translations.Common_Unknown,
        Vendor = GpuVendor.Unknown,
    };

    /// <summary>
    ///     The display name of the GPU (e.g., "NVIDIA GeForce RTX 4090").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     The driver version string.
    /// </summary>
    public required string DriverVersion { get; init; }

    /// <summary>
    ///     The GPU vendor manufacturer.
    /// </summary>
    public required GpuVendor Vendor { get; init; }

    /// <summary>
    ///     The dedicated video memory in megabytes, or <c>null</c> if unknown.
    /// </summary>
    public int? MemoryMB { get; init; }

    /// <summary>
    ///     The PnP device ID from WMI, or <c>null</c> if unavailable.
    /// </summary>
    public string? DeviceId { get; init; }

    /// <summary>
    ///     The Plug and Play device ID, or <c>null</c> if unavailable.
    /// </summary>
    public string? PnpDeviceId { get; init; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"GPU: {Name}, Driver: {DriverVersion}, Vendor: {Vendor}, Memory: {MemoryMB} MB, DeviceId: {DeviceId}, PnpDeviceId: {PnpDeviceId}";
    }
}

/// <summary>
///     Represents information about the CPU.
/// </summary>
public sealed record CpuInfo
{
    /// <summary>
    ///     A sentinel value representing an unknown CPU.
    /// </summary>
    public static readonly CpuInfo Unknown = new()
    {
        Name = Translations.Common_Unknown,
        Manufacturer = Translations.Common_Unknown,
        Vendor = Translations.Common_Unknown,
        Architecture = Translations.Common_Unknown,
        Cores = 0,
        Threads = 0,
        MaxClockMHz = 0,
        CurrentClockMHz = 0,
        L2CacheKB = 0,
        L3CacheKB = 0,
    };

    /// <summary>
    ///     The processor display name (e.g., "Intel Core i9-13900K").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     The manufacturer identifier (e.g., "GenuineIntel").
    /// </summary>
    public required string Manufacturer { get; init; }

    /// <summary>
    ///     The detected vendor string (e.g., "Intel", "AMD").
    /// </summary>
    public required string Vendor { get; init; }

    /// <summary>
    ///     The processor architecture (e.g., "64-bit").
    /// </summary>
    public required string Architecture { get; init; }

    /// <summary>
    ///     The number of physical cores.
    /// </summary>
    public required int Cores { get; init; }

    /// <summary>
    ///     The number of logical threads.
    /// </summary>
    public required int Threads { get; init; }

    /// <summary>
    ///     The maximum clock speed in MHz.
    /// </summary>
    public required int MaxClockMHz { get; init; }

    /// <summary>
    ///     The current clock speed in MHz.
    /// </summary>
    public required int CurrentClockMHz { get; init; }

    /// <summary>
    ///     The L2 cache size in kilobytes.
    /// </summary>
    public required int L2CacheKB { get; init; }

    /// <summary>
    ///     The L3 cache size in kilobytes.
    /// </summary>
    public required int L3CacheKB { get; init; }
}

/// <summary>
///     Represents information about a disk volume.
/// </summary>
public sealed record DiskVolume
{
    /// <summary>
    ///     The drive letter (e.g., "C:").
    /// </summary>
    public required string DriveLetter { get; init; }

    /// <summary>
    ///     Indicates whether this is the Windows system drive.
    /// </summary>
    public required bool IsSystemDrive { get; init; }

    /// <summary>
    ///     The file system format (e.g., "NTFS").
    /// </summary>
    public required string DriveFormat { get; init; }

    /// <summary>
    ///     The drive type (e.g., "Fixed", "Removable").
    /// </summary>
    public required string DriveType { get; init; }

    /// <summary>
    ///     The volume label.
    /// </summary>
    public required string VolumeLabel { get; init; }

    /// <summary>
    ///     The total disk size in gigabytes.
    /// </summary>
    public required double TotalSizeGB { get; init; }

    /// <summary>
    ///     The available free space in gigabytes.
    /// </summary>
    public required double AvailableSizeGB { get; init; }

    /// <summary>
    ///     The used space in gigabytes.
    /// </summary>
    public required double UsedSizeGB { get; init; }

    /// <summary>
    ///     The percentage of disk space used.
    /// </summary>
    public required double UsedPercent { get; init; }

    /// <summary>
    ///     The storage media type: "SSD", "HDD", or "Unknown".
    /// </summary>
    public required string MediaType { get; init; }

    /// <summary>
    ///     Indicates whether the drive is removable (e.g., USB).
    /// </summary>
    public required bool IsRemovable { get; init; }

    /// <summary>
    ///     The disk serial number, or <c>null</c> if unavailable.
    /// </summary>
    public string? SerialNumber { get; init; }

    /// <summary>
    ///     The disk model name, or <c>null</c> if unavailable.
    /// </summary>
    public string? Model { get; init; }
}

/// <summary>
///     Represents information about all disk volumes.
/// </summary>
public sealed record DiskInfo
{
    /// <summary>
    ///     A sentinel value representing unknown disk information.
    /// </summary>
    public static readonly DiskInfo Unknown = new() { Volumes = [] };

    /// <summary>
    ///     The list of detected disk volumes.
    /// </summary>
    public required IReadOnlyList<DiskVolume> Volumes { get; init; }
}

/// <summary>
///     Represents a physical RAM module.
/// </summary>
public sealed record RamModule
{
    /// <summary>
    ///     The module capacity in gigabytes.
    /// </summary>
    public required double CapacityGB { get; init; }

    /// <summary>
    ///     The memory speed in MHz.
    /// </summary>
    public required string SpeedMHz { get; init; }

    /// <summary>
    ///     The module manufacturer.
    /// </summary>
    public required string Manufacturer { get; init; }

    /// <summary>
    ///     The module part number.
    /// </summary>
    public required string PartNumber { get; init; }

    /// <summary>
    ///     The physical slot or device locator string.
    /// </summary>
    public required string DeviceLocator { get; init; }
}

/// <summary>
///     Represents information about RAM.
/// </summary>
public sealed record RamInfo
{
    /// <summary>
    ///     A sentinel value representing unknown RAM information.
    /// </summary>
    public static readonly RamInfo Unknown = new()
    {
        TotalGB = 0,
        TotalMB = 0,
        TotalKB = 0,
        AvailableGB = 0,
        UsedPercent = 0,
        UsedGB = 0,
        Modules = [],
    };

    /// <summary>
    ///     The total installed RAM in gigabytes.
    /// </summary>
    public required double TotalGB { get; init; }

    /// <summary>
    ///     The total installed RAM in megabytes.
    /// </summary>
    public required long TotalMB { get; init; }

    /// <summary>
    ///     The total installed RAM in kilobytes.
    /// </summary>
    public required long TotalKB { get; init; }

    /// <summary>
    ///     The available (free) RAM in gigabytes.
    /// </summary>
    public required double AvailableGB { get; init; }

    /// <summary>
    ///     The percentage of RAM currently in use.
    /// </summary>
    public required double UsedPercent { get; init; }

    /// <summary>
    ///     The amount of RAM currently in use in gigabytes.
    /// </summary>
    public required double UsedGB { get; init; }

    /// <summary>
    ///     The list of physical RAM modules.
    /// </summary>
    public required IReadOnlyList<RamModule> Modules { get; init; }
}

/// <summary>
///     Represents information about the operating system.
/// </summary>
public sealed record OsInfo
{
    /// <summary>
    ///     A sentinel value representing unknown OS information.
    /// </summary>
    public static readonly OsInfo Unknown = new()
    {
        Name = Translations.Common_Unknown,
        Version = Translations.Common_Unknown,
        BuildNumber = Translations.Common_Unknown,
        Edition = Translations.Common_Unknown,
        Architecture = Translations.Common_Unknown,
        DeviceType = Translations.Common_Unknown,
        InstallDate = Translations.Common_Unknown,
        LastBootUpTime = Translations.Common_Unknown,
    };

    /// <summary>
    ///     The OS name (e.g., "Microsoft Windows 11 Pro").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     The OS version string.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    ///     The OS build number.
    /// </summary>
    public required string BuildNumber { get; init; }

    /// <summary>
    ///     The OS edition (e.g., "Pro", "Home").
    /// </summary>
    public required string Edition { get; init; }

    /// <summary>
    ///     The OS architecture (e.g., "64-bit").
    /// </summary>
    public required string Architecture { get; init; }

    /// <summary>
    ///     The device type inferred from chassis information (e.g., "Desktop", "Laptop").
    /// </summary>
    public required string DeviceType { get; init; }

    /// <summary>
    ///     The OS install date.
    /// </summary>
    public required string InstallDate { get; init; }

    /// <summary>
    ///     The last boot-up time.
    /// </summary>
    public required string LastBootUpTime { get; init; }
}

/// <summary>
///     Represents information about the system BIOS.
/// </summary>
public sealed record BiosInfo
{
    /// <summary>
    ///     A sentinel value representing unknown BIOS information.
    /// </summary>
    public static readonly BiosInfo Unknown = new()
    {
        Manufacturer = Translations.Common_Unknown,
        Version = Translations.Common_Unknown,
        ReleaseDate = Translations.Common_Unknown,
        SmbiosVersion = Translations.Common_Unknown,
        SerialNumber = Translations.Common_Unknown,
    };

    /// <summary>
    ///     The BIOS manufacturer.
    /// </summary>
    public required string Manufacturer { get; init; }

    /// <summary>
    ///     The BIOS version string.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    ///     The BIOS release date.
    /// </summary>
    public required string ReleaseDate { get; init; }

    /// <summary>
    ///     The SMBIOS version.
    /// </summary>
    public required string SmbiosVersion { get; init; }

    /// <summary>
    ///     The system serial number.
    /// </summary>
    public required string SerialNumber { get; init; }
}

/// <summary>
///     Represents a complete snapshot of the system's hardware and software information.
/// </summary>
public sealed record SystemSnapshot
{
    /// <summary>
    ///     A sentinel value representing a completely unknown system.
    /// </summary>
    public static readonly SystemSnapshot Unknown = new()
    {
        Cpu = CpuInfo.Unknown,
        Ram = RamInfo.Unknown,
        Os = OsInfo.Unknown,
        Bios = BiosInfo.Unknown,
        Gpus = [],
        PrimaryGpu = null,
        Disk = DiskInfo.Unknown,
    };

    /// <summary>
    ///     CPU information.
    /// </summary>
    public required CpuInfo Cpu { get; init; }

    /// <summary>
    ///     RAM information.
    /// </summary>
    public required RamInfo Ram { get; init; }

    /// <summary>
    ///     Operating system information.
    /// </summary>
    public required OsInfo Os { get; init; }

    /// <summary>
    ///     BIOS information.
    /// </summary>
    public required BiosInfo Bios { get; init; }

    /// <summary>
    ///     List of all detected GPUs.
    /// </summary>
    public required IReadOnlyList<GpuInfo> Gpus { get; init; }

    /// <summary>
    ///     The primary GPU (typically the one with the most VRAM), or <c>null</c> if none detected.
    /// </summary>
    public GpuInfo? PrimaryGpu { get; init; }

    /// <summary>
    ///     Disk volume information.
    /// </summary>
    public required DiskInfo Disk { get; init; }
}

// ============================================================================
// WMI HELPER (with connection caching)
// ============================================================================

internal static class WmiHelper
{
    private static readonly Dictionary<string, ManagementScope> ScopeCache = new(
        StringComparer.OrdinalIgnoreCase
    );
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

    public static IEnumerable<ManagementObject> Query(
        string query,
        string namespacePath = @"root\cimv2"
    )
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

    /// <summary>
    ///     Gets an string property from a ManagementObject with fallback.
    /// </summary>
    /// <param name="mo">The ManagementObject to query.</param>
    /// <param name="property">The property name to retrieve.</param>
    /// <param name="fallback">The fallback value if property is null or missing.</param>
    /// <returns>The property value or fallback.</returns>
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
                _ => value.ToString()?.Trim() ?? fallback,
            };
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>
    ///     Gets an integer property from a ManagementObject with fallback.
    /// </summary>
    /// <param name="mo">The ManagementObject to query.</param>
    /// <param name="property">The property name to retrieve.</param>
    /// <param name="fallback">The fallback value if property is null or missing.</param>
    /// <returns>The property value or fallback.</returns>
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

    /// <summary>
    ///     Gets a long integer property from a ManagementObject with fallback.
    /// </summary>
    /// <param name="mo">The ManagementObject to query.</param>
    /// <param name="property">The property name to retrieve.</param>
    /// <param name="fallback">The fallback value if property is null or missing.</param>
    /// <returns>The property value or fallback.</returns>
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

    /// <summary>
    ///     Returns current memory status using the same API Task Manager uses.
    /// </summary>
    public static MEMORYSTATUSEX? GetMemoryStatus()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        return GlobalMemoryStatusEx(ref status) ? status : null;
    }

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

            using (
                var cpuKey = Registry.LocalMachine.OpenSubKey(
                    @"HARDWARE\DESCRIPTION\System\CentralProcessor\0"
                )
            )
            {
                if (cpuKey != null)
                {
                    name = cpuKey.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? name;
                    manufacturer =
                        cpuKey.GetValue("VendorIdentifier")?.ToString()?.Trim() ?? manufacturer;
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
                "SELECT NumberOfCores, MaxClockSpeed, L2CacheSize, L3CacheSize FROM Win32_Processor"
            );
            if (cpu != null)
            {
                cores = WmiHelper.GetInt(cpu, "NumberOfCores");
                maxMHz = WmiHelper.GetInt(cpu, "MaxClockSpeed", maxMHz);
                l2kb = WmiHelper.GetInt(cpu, "L2CacheSize");
                l3kb = WmiHelper.GetInt(cpu, "L3CacheSize");
            }

            if (cores == 0)
                cores = threads; // fallback

            var vendor = DetectCpuVendor(manufacturer);
            var architecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";

            return new CpuInfo
            {
                Name = name,
                Manufacturer = manufacturer,
                Vendor = vendor,
                Architecture = architecture,
                Cores = cores,
                Threads = threads,
                MaxClockMHz = maxMHz,
                CurrentClockMHz = currentMHz,
                L2CacheKB = l2kb,
                L3CacheKB = l3kb,
            };
        }
        catch
        {
            return CpuInfo.Unknown;
        }
    }

    private static string DetectCpuVendor(string manufacturer)
    {
        var lower = manufacturer.ToLowerInvariant();
        if (lower.Contains("intel") || lower.Contains("genuineintel"))
            return "Intel";
        if (lower.Contains("amd") || lower.Contains("authenticamd"))
            return "AMD";
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
            var (totalGB, totalMB, totalKB, availableGB, usedPercent, usedGB) = GetMemoryStats(
                modules
            );

            return new RamInfo
            {
                TotalGB = totalGB,
                TotalMB = totalMB,
                TotalKB = totalKB,
                AvailableGB = availableGB,
                UsedPercent = usedPercent,
                UsedGB = usedGB,
                Modules = modules,
            };
        }
        catch
        {
            return RamInfo.Unknown;
        }
    }

    private static List<RamModule> GetPhysicalModules()
    {
        var modules = new List<RamModule>();
        var physicalMemory = WmiHelper.Query(
            "SELECT Capacity, Speed, Manufacturer, PartNumber, DeviceLocator FROM Win32_PhysicalMemory"
        );

        foreach (var mem in physicalMemory)
        {
            var capacityBytes = WmiHelper.GetLong(mem, "Capacity");
            var capacityGB = capacityBytes > 0 ? capacityBytes / (1024.0 * 1024.0 * 1024.0) : 0;
            var speed = WmiHelper.GetString(mem, "Speed");
            var manufacturer = WmiHelper.GetString(mem, "Manufacturer");
            var partNumber = WmiHelper.GetString(mem, "PartNumber");
            var deviceLocator = WmiHelper.GetString(mem, "DeviceLocator");

            modules.Add(
                new RamModule
                {
                    CapacityGB = Math.Round(capacityGB, 2),
                    SpeedMHz = speed,
                    Manufacturer = manufacturer,
                    PartNumber = partNumber,
                    DeviceLocator = deviceLocator,
                }
            );
        }

        return modules;
    }

    private static (
        double totalGB,
        long totalMB,
        long totalKB,
        double availableGB,
        double usedPercent,
        double usedGB
    ) GetMemoryStats(List<RamModule> modules)
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
        if (fallbackTotalGB <= 0)
            fallbackTotalGB = 0;

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
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped
    );

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

            using var handle = CreateFile(
                @"\\.\" + trimmedDrive,
                GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero
            );

            if (handle.IsInvalid)
                return "Unknown";

            var query = new STORAGE_PROPERTY_QUERY
            {
                PropertyId = STORAGE_PROPERTY_ID.StorageDeviceSeekPenaltyProperty,
                QueryType = STORAGE_QUERY_TYPE.PropertyStandardQuery,
                AdditionalParameters = new byte[1],
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

                var success = DeviceIoControl(
                    handle,
                    IOCTL_STORAGE_QUERY_PROPERTY,
                    queryPtr,
                    (uint)querySize,
                    resultPtr,
                    (uint)resultSize,
                    out _,
                    IntPtr.Zero
                );

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
                $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetter.TrimEnd('\\')}' }} WHERE AssocClass=Win32_LogicalDiskToPartition"
            );

            foreach (var partition in partitions)
            {
                var diskDrives = WmiHelper.Query(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition"
                );

                foreach (var disk in diskDrives)
                {
                    var mediaType = WmiHelper.GetString(disk, "MediaType", "").ToLowerInvariant();
                    var model = WmiHelper.GetString(disk, "Model", "").ToLowerInvariant();

                    // Check for SSD indicators
                    if (
                        mediaType.Contains("ssd")
                        || model.Contains("ssd")
                        || model.Contains("nvme")
                        || mediaType.Contains("fixed hard disk media")
                    )
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
        return string.Equals(
            systemDrive,
            driveLetter.TrimEnd('\\'),
            StringComparison.OrdinalIgnoreCase
        );
    }

    private enum STORAGE_PROPERTY_ID
    {
        StorageDeviceSeekPenaltyProperty = 7,
    }

    private enum STORAGE_QUERY_TYPE
    {
        PropertyStandardQuery = 0,
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

        [MarshalAs(UnmanagedType.U1)]
        public bool IncursSeekPenalty;
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

            var volumes = DriveInfo
                .GetDrives()
                .Where(d => d.IsReady)
                .Select(drive =>
                {
                    var totalBytes = drive.TotalSize;
                    var freeBytes = drive.AvailableFreeSpace;
                    var usedBytes = totalBytes - freeBytes;

                    var isSystemDrive = DiskHelper.IsSystemDrive(drive.Name);
                    var isRemovable = drive.DriveType == DriveType.Removable;

                    // Try to get disk info for this drive (model, serial, media type)
                    var diskInfo = GetDiskInfoForDrive(drive.Name, diskNumberToInfo);

                    // Media type priority: PhysicalDisk info > seek penalty P/Invoke > WMI fallback
                    var mediaType = diskInfo?.MediaType ?? DiskHelper.DetectMediaType(drive.Name);

                    return new DiskVolume
                    {
                        DriveLetter = drive.Name.TrimEnd('\\'),
                        IsSystemDrive = isSystemDrive,
                        DriveFormat = drive.DriveFormat,
                        DriveType = drive.DriveType.ToString(),
                        VolumeLabel = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                            ? "Local Disk"
                            : drive.VolumeLabel,
                        TotalSizeGB = ToGb(totalBytes),
                        AvailableSizeGB = ToGb(freeBytes),
                        UsedSizeGB = ToGb(usedBytes),
                        UsedPercent =
                            totalBytes > 0 ? Math.Round(usedBytes * 100.0 / totalBytes, 1) : 0,
                        MediaType = mediaType,
                        IsRemovable = isRemovable,
                        SerialNumber = diskInfo?.SerialNumber,
                        Model = diskInfo?.Model,
                    };
                })
                .ToList();

            return new DiskInfo { Volumes = volumes };
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
        if (disks.Count > 0)
            return disks;

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
                @"root\Microsoft\Windows\Storage"
            );

            foreach (var disk in physicalDisks)
            {
                var mediaTypeValue = WmiHelper.GetInt(disk, "MediaType");
                var mediaType = mediaTypeValue switch
                {
                    3 => "HDD",
                    4 => "SSD",
                    5 => "SCM", // Storage Class Memory
                    _ => "Unknown",
                };

                disks.Add(
                    new PhysicalDiskInfo
                    {
                        DiskNumber = WmiHelper.GetString(disk, "DeviceId", "-1"),
                        Model = WmiHelper.GetString(disk, "FriendlyName"),
                        SerialNumber = WmiHelper.GetString(disk, "SerialNumber").Trim(),
                        MediaType = mediaType,
                        DeviceID = "", // Not available from MSFT_PhysicalDisk
                    }
                );
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
            var diskDrives = WmiHelper.Query(
                "SELECT DeviceID, Model, SerialNumber, MediaType, Index FROM Win32_DiskDrive"
            );
            foreach (var disk in diskDrives)
                disks.Add(
                    new PhysicalDiskInfo
                    {
                        DeviceID = WmiHelper.GetString(disk, "DeviceID"),
                        DiskNumber = WmiHelper.GetInt(disk, "Index").ToString(),
                        Model = WmiHelper.GetString(disk, "Model"),
                        SerialNumber = WmiHelper.GetString(disk, "SerialNumber").Trim(),
                        MediaType = "", // Win32_DiskDrive MediaType is unreliable
                    }
                );
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
    private static Dictionary<int, PhysicalDiskInfo> BuildDiskNumberMap(
        List<PhysicalDiskInfo> disks
    )
    {
        var map = new Dictionary<int, PhysicalDiskInfo>();
        foreach (var disk in disks)
            if (int.TryParse(disk.DiskNumber, out var num))
                map.TryAdd(num, disk);

        return map;
    }

    /// <summary>
    ///     Maps a logical drive letter to a physical disk using Win32 associations.
    /// </summary>
    private static PhysicalDiskInfo? GetDiskInfoForDrive(
        string driveLetter,
        Dictionary<int, PhysicalDiskInfo> diskNumberMap
    )
    {
        if (diskNumberMap.Count == 0)
            return null;

        try
        {
            var letter = driveLetter.TrimEnd('\\');

            // Try MSFT_Partition to get DiskNumber for this drive letter
            var partitions = WmiHelper.Query(
                $"SELECT DiskNumber FROM MSFT_Partition WHERE DriveLetter = '{letter.TrimEnd(':')}'",
                @"root\Microsoft\Windows\Storage"
            );

            foreach (var partition in partitions)
            {
                var diskNum = WmiHelper.GetInt(partition, "DiskNumber", -1);
                if (diskNum >= 0 && diskNumberMap.TryGetValue(diskNum, out var info))
                    return info;
            }

            // Fallback: classic WMI ASSOCIATORS path
            var classicPartitions = WmiHelper.Query(
                $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{letter}'}} WHERE AssocClass=Win32_LogicalDiskToPartition"
            );

            foreach (var partition in classicPartitions)
            {
                var diskDrives = WmiHelper.Query(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition"
                );

                foreach (var disk in diskDrives)
                {
                    var index = WmiHelper.GetInt(disk, "Index", -1);
                    if (index >= 0 && diskNumberMap.TryGetValue(index, out var info))
                        return info;

                    // Direct match by DeviceID
                    var deviceId = WmiHelper.GetString(disk, "DeviceID");
                    foreach (var entry in diskNumberMap.Values)
                        if (entry.DeviceID == deviceId)
                            return entry;
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
    // DXGI_ADAPTER_FLAG values
    public const uint DXGI_ADAPTER_FLAG_SOFTWARE = 2;

    // Microsoft Basic Render Driver identifiers
    private const uint MICROSOFT_VENDOR_ID = 0x1414;

    private const uint BASIC_RENDER_DEVICE_ID = 0x8C;
    private static readonly Guid IID_IDXGIFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");

    [DllImport("dxgi.dll", PreserveSig = false)]
    private static extern void CreateDXGIFactory1(
        [In] ref Guid riid,
        [Out] [MarshalAs(UnmanagedType.Interface)] out IDXGIFactory1 factory
    );

    public static IDXGIFactory1 CreateFactory()
    {
        var iid = IID_IDXGIFactory1;
        CreateDXGIFactory1(ref iid, out var factory);
        return factory;
    }

    public static bool IsSoftwareAdapter(DXGI_ADAPTER_DESC1 desc)
    {
        if ((desc.Flags & DXGI_ADAPTER_FLAG_SOFTWARE) != 0)
            return true;

        // Microsoft Basic Render Driver
        return desc.VendorId == MICROSOFT_VENDOR_ID && desc.DeviceId == BASIC_RENDER_DEVICE_ID;
    }

    [ComImport]
    [Guid("770aae78-f26f-4dba-a829-253c83d1b387")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDXGIFactory1
    {
        // IDXGIObject (4 methods)
        void SetPrivateData([In] ref Guid Name, uint DataSize, IntPtr pData);

        void SetPrivateDataInterface(
            [In] ref Guid Name,
            [MarshalAs(UnmanagedType.IUnknown)] object pUnknown
        );

        void GetPrivateData([In] ref Guid Name, ref uint pDataSize, IntPtr pData);

        void GetParent([In] ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppParent);

        // IDXGIFactory (4 methods)
        void EnumAdapters(uint Adapter, [MarshalAs(UnmanagedType.IUnknown)] out object ppAdapter);

        void MakeWindowAssociation(IntPtr WindowHandle, uint Flags);

        void GetWindowAssociation(out IntPtr pWindowHandle);

        void CreateSwapChain(
            [MarshalAs(UnmanagedType.IUnknown)] object pDevice,
            IntPtr pDesc,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppSwapChain
        );

        void CreateSoftwareAdapter(
            IntPtr Module,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppAdapter
        );

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

        void SetPrivateDataInterface(
            [In] ref Guid Name,
            [MarshalAs(UnmanagedType.IUnknown)] object pUnknown
        );

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
        if (gpus.Count == 0)
            return null;
        if (gpus.Count == 1)
            return gpus[0];

        return gpus.OrderByDescending(g => g.MemoryMB ?? 0)
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
                if (hr != 0 || adapter == null)
                    break; // DXGI_ERROR_NOT_FOUND

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

                    gpus.Add(
                        new GpuInfo
                        {
                            Name = name,
                            DriverVersion = wmiMatch?.DriverVersion ?? Translations.Common_Unknown,
                            Vendor = vendor,
                            MemoryMB = memoryMB > 0 ? memoryMB : null,
                            DeviceId = wmiMatch?.DeviceId,
                            PnpDeviceId = wmiMatch?.PnpDeviceId,
                        }
                    );
                }
                finally
                {
                    Marshal.ReleaseComObject(adapter);
                }
            }
        }
        finally
        {
            if (factory != null)
                Marshal.ReleaseComObject(factory);
        }

        return gpus.Count > 0 ? gpus : [GpuInfo.Unknown];
    }

    // ── WMI fallback path ──────────────────────────────────────────────────

    private static IReadOnlyList<GpuInfo> GetAllViaWmi()
    {
        var gpus = new List<GpuInfo>();
        var controllers = WmiHelper.Query(
            "SELECT Name, DriverVersion, DeviceID, PNPDeviceID, AdapterRAM FROM Win32_VideoController"
        );

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

            gpus.Add(
                new GpuInfo
                {
                    Name = name,
                    DriverVersion = driver,
                    Vendor = vendor,
                    MemoryMB = memoryMB,
                    DeviceId = deviceId,
                    PnpDeviceId = pnpId,
                }
            );
        }

        return gpus.Count > 0 ? gpus : [GpuInfo.Unknown];
    }

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
                "SELECT Name, DriverVersion, DeviceID, PNPDeviceID FROM Win32_VideoController"
            );

            foreach (var controller in controllers)
            {
                var name = WmiHelper.GetString(controller, "Name", "");
                var driver = WmiHelper.GetString(controller, "DriverVersion");
                var deviceId = WmiHelper.GetString(controller, "DeviceID");
                var pnpId = WmiHelper.GetString(controller, "PNPDeviceID");

                // Extract VendorId and DeviceId from PNP DeviceID (e.g., PCI\VEN_10DE&DEV_2684&...)
                ParsePnpIds(pnpId, out var vendorId, out var hardwareDeviceId);

                entries.Add(
                    new WmiGpuEntry(name, driver, deviceId, pnpId, vendorId, hardwareDeviceId)
                );
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
        if (string.IsNullOrWhiteSpace(pnpId))
            return;

        var upper = pnpId.ToUpperInvariant();

        var venIdx = upper.IndexOf("VEN_", StringComparison.Ordinal);
        if (venIdx >= 0 && venIdx + 8 <= upper.Length)
            if (
                uint.TryParse(
                    upper.Substring(venIdx + 4, 4),
                    NumberStyles.HexNumber,
                    null,
                    out var v
                )
            )
                vendorId = v;

        var devIdx = upper.IndexOf("DEV_", StringComparison.Ordinal);
        if (devIdx >= 0 && devIdx + 8 <= upper.Length)
            if (
                uint.TryParse(
                    upper.Substring(devIdx + 4, 4),
                    NumberStyles.HexNumber,
                    null,
                    out var d
                )
            )
                hardwareDeviceId = d;
    }

    /// <summary>
    ///     Matches a DXGI adapter to its WMI entry.
    ///     Priority: VendorId+DeviceId match > name substring match.
    /// </summary>
    private static WmiGpuEntry? FindWmiMatch(
        string dxgiName,
        uint dxgiVendorId,
        uint dxgiDeviceId,
        List<WmiGpuEntry> wmiEntries
    )
    {
        // First pass: match by hardware IDs (most reliable)
        foreach (var entry in wmiEntries)
            if (entry.VendorId == dxgiVendorId && entry.HardwareDeviceId == dxgiDeviceId)
                return entry;

        // Second pass: match by name substring
        var dxgiNameLower = dxgiName.ToLowerInvariant();
        foreach (var entry in wmiEntries)
            if (
                !string.IsNullOrEmpty(entry.Name)
                && dxgiNameLower.Contains(entry.Name.ToLowerInvariant())
            )
                return entry;

        return null;
    }

    // ── Shared helpers ─────────────────────────────────────────────────────

    private static bool IsVirtualAdapter(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.Contains("microsoft basic")
            || lower.Contains("remote desktop")
            || lower.Contains("virtual");
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
            _ => GpuVendor.Unknown,
        };
    }

    /// <summary>
    ///     Detects GPU vendor from name and PNP DeviceID strings (WMI fallback path).
    /// </summary>
    private static GpuVendor DetectGpuVendor(string name, string? pnpId)
    {
        var nameLower = name.ToLowerInvariant();
        var pnpLower = pnpId?.ToLowerInvariant() ?? "";

        if (
            nameLower.Contains("nvidia")
            || nameLower.Contains("geforce")
            || nameLower.Contains("quadro")
            || nameLower.Contains("tesla")
            || pnpLower.Contains("ven_10de")
        )
            return GpuVendor.NVIDIA;

        if (
            nameLower.Contains("amd")
            || nameLower.Contains("radeon")
            || nameLower.Contains("ryzen")
            || pnpLower.Contains("ven_1002")
        )
            return GpuVendor.AMD;

        if (
            nameLower.Contains("intel")
            || nameLower.Contains("iris")
            || nameLower.Contains("uhd")
            || pnpLower.Contains("ven_8086")
        )
            return GpuVendor.Intel;

        return GpuVendor.Unknown;
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

            using (
                var ntKey = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion"
                )
            )
            {
                if (ntKey != null)
                {
                    buildNumber =
                        ntKey.GetValue("CurrentBuildNumber")?.ToString()
                        ?? ntKey.GetValue("CurrentBuild")?.ToString()
                        ?? buildNumber;
                    edition = ntKey.GetValue("EditionID")?.ToString() ?? edition;
                    displayVersion = ntKey.GetValue("DisplayVersion")?.ToString() ?? "";

                    // Install date from Registry (Unix timestamp)
                    if (
                        ntKey.GetValue("InstallDate") is int installTimestamp
                        && installTimestamp > 0
                    )
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

            return new OsInfo
            {
                Name = name,
                Version = version,
                BuildNumber = buildNumber,
                Edition = edition,
                Architecture = architecture,
                DeviceType = deviceType,
                InstallDate = installDate,
                LastBootUpTime = lastBoot,
            };
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
            _ => "Unknown",
        };
    }

    private static string GetDeviceType()
    {
        var chassis = WmiHelper.GetFirst("SELECT ChassisTypes FROM Win32_SystemEnclosure");
        if (chassis == null)
            return "Unknown";

        try
        {
            return chassis["ChassisTypes"] is ushort[] { Length: > 0 } types
                ? MapChassisType(types)
                : "Unknown";
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

                case >= 1
                and <= 7
                or 12
                or 13
                or >= 15
                and <= 29
                or >= 33
                and <= 36:
                    return Translations.Dashboard_SystemInfo_Os_DeviceType_Desktop;
            }

        return Translations.Common_Unknown;
    }

    private static string FormatWmiDateTime(string wmiDate)
    {
        if (wmiDate.Length < 12)
            return "Unknown";
        return $"{wmiDate[..4]}-{wmiDate.Substring(4, 2)}-{wmiDate.Substring(6, 2)} {wmiDate.Substring(8, 2)}:{wmiDate.Substring(10, 2)}";
    }
}

internal static class BiosProvider
{
    public static BiosInfo Get()
    {
        try
        {
            var bios = WmiHelper.GetFirst(
                "SELECT Manufacturer, BIOSVersion, SMBIOSBIOSVersion, SerialNumber, ReleaseDate FROM Win32_BIOS"
            );
            if (bios == null)
                return BiosInfo.Unknown;

            var manufacturer = WmiHelper.GetString(bios, "Manufacturer");
            var version = WmiHelper.GetString(bios, "BIOSVersion");
            var smbiosVersion = WmiHelper.GetString(bios, "SMBIOSBIOSVersion");
            var serialNumber = WmiHelper.GetString(bios, "SerialNumber");
            var releaseDate = FormatWmiDate(WmiHelper.GetString(bios, "ReleaseDate", ""));

            return new BiosInfo
            {
                Manufacturer = manufacturer,
                Version = version,
                ReleaseDate = releaseDate,
                SmbiosVersion = smbiosVersion,
                SerialNumber = serialNumber,
            };
        }
        catch
        {
            return BiosInfo.Unknown;
        }
    }

    private static string FormatWmiDate(string wmiDate)
    {
        if (wmiDate.Length < 8)
            return "Unknown";
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

                Snapshot = new SystemSnapshot
                {
                    Cpu = _cachedCpu,
                    Ram = await ramTask,
                    Os = _cachedOs,
                    Bios = _cachedBios,
                    Gpus = _cachedGpus,
                    PrimaryGpu = primaryGpu,
                    Disk = await diskTask,
                };
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

                Snapshot = new SystemSnapshot
                {
                    Cpu = _cachedCpu!,
                    Ram = await ramTask,
                    Os = _cachedOs!,
                    Bios = _cachedBios!,
                    Gpus = gpus,
                    PrimaryGpu = primaryGpu,
                    Disk = await diskTask,
                };
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
            _logger.LogInformation(
                "OS : {OsName} {OsEdition} [{OsArchitecture}] ({OsDeviceType})",
                Snapshot.Os.Name,
                Snapshot.Os.Edition,
                Snapshot.Os.Architecture,
                Snapshot.Os.DeviceType
            );

            LogGpuSummary();

            _logger.LogInformation(
                "CPU: {CpuName} [{CpuVendor}] ({CpuCores} Cores/{CpuThreads} Threads)",
                Snapshot.Cpu.Name,
                Snapshot.Cpu.Vendor,
                Snapshot.Cpu.Cores,
                Snapshot.Cpu.Threads
            );

            _logger.LogInformation(
                "RAM: {RamTotalGb:F1} GB [{RamModules} Module(s)] (Used: {RamUsedGB:F1} GB)",
                Snapshot.Ram.TotalGB,
                Snapshot.Ram.Modules.Count,
                Snapshot.Ram.UsedGB
            );

            foreach (var volume in Snapshot.Disk.Volumes)
            {
                var systemDrive = volume.IsSystemDrive ? " [System Drive]" : "";
                var modelInfo = !string.IsNullOrEmpty(volume.Model) ? $" - {volume.Model}" : "";
                _logger.LogInformation(
                    "Disk {VolumeLetter}{SystemDrive} [{MediaType}] {VolumeTotalSizeGb:F1} GB (Free: {VolumeFreeSpaceGb:F1} GB){ModelInfo}",
                    volume.DriveLetter,
                    systemDrive,
                    volume.MediaType,
                    volume.TotalSizeGB,
                    volume.AvailableSizeGB,
                    modelInfo
                );
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
            _logger.LogInformation(
                "GPU: {GpuName} [{GpuVendor}]{MemoryInfo}",
                gpu.Name,
                gpu.Vendor,
                memoryInfo
            );
            return;
        }

        // Multiple GPUs
        if (Snapshot.PrimaryGpu != null)
        {
            var memoryInfoPrimary = Snapshot.PrimaryGpu.MemoryMB.HasValue
                ? $" ({Snapshot.PrimaryGpu.MemoryMB.Value / 1024.0:F0} GB)"
                : "";
            _logger.LogInformation(
                "GPU (Primary): {PrimaryGpuName} [{PrimaryGpuVendor}]{MemoryInfo}",
                Snapshot.PrimaryGpu.Name,
                Snapshot.PrimaryGpu.Vendor,
                memoryInfoPrimary
            );

            for (var index = 0; index < Snapshot.Gpus.Count; index++)
            {
                if (
                    Snapshot.Gpus[index].Name == Snapshot.PrimaryGpu.Name
                    && Snapshot.Gpus[index].Vendor == Snapshot.PrimaryGpu.Vendor
                )
                    continue;

                var gpu = Snapshot.Gpus[index];
                var memoryInfo = gpu.MemoryMB.HasValue
                    ? $" [{gpu.MemoryMB.Value / 1024.0:F0} GB]"
                    : "";
                _logger.LogInformation(
                    "GPU ({GpuIndex}): {GpuName} ({GpuVendor}){MemoryInfo}",
                    index + 1,
                    gpu.Name,
                    gpu.Vendor,
                    memoryInfo
                );
            }

            _logger.LogInformation("Total GPUs : {GpuCount}", gpuCount);
        }
    }
}
