using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using DotNetArch = System.Runtime.InteropServices.Architecture;
using WmiEnumerationOptions = System.Management.EnumerationOptions;

namespace optimizerDuck.Services.System;

// ============================================================================
// WMI HELPER (connection caching, always-disposing queries)
// ============================================================================

internal static class WmiHelper
{
    private static readonly ConcurrentDictionary<string, ManagementScope> ScopeCache = new(
        StringComparer.OrdinalIgnoreCase
    );

    private static readonly WmiEnumerationOptions DefaultOptions = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        ReturnImmediately = false,
    };

    public static void Initialize()
    {
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        try
        {
            DisposeScopes();
        }
        catch
        {
            // Ignore errors during process exit
        }
    }

    private static ManagementScope GetScope(string namespacePath = @"root\cimv2")
    {
        return ScopeCache.GetOrAdd(
            namespacePath,
            static ns =>
            {
                var scope = new ManagementScope(ns);
                scope.Connect();
                return scope;
            }
        );
    }

    public static void DisposeScopes()
    {
        ScopeCache.Clear();
    }

    // static log hook (same pattern as PowerReader.Configure); per-instance wiring if a second writer appears.
    private static Action<string, Exception>? _onQueryFailed;

    /// <summary>Wires a debug sink for failed WMI queries. Null detaches.</summary>
    internal static void ConfigureFailureLogger(Action<string, Exception>? handler)
    {
        _onQueryFailed = handler;
    }

    private static void ReportFailure(string query, string namespacePath, Exception ex)
    {
        try
        {
            _onQueryFailed?.Invoke($"{query} [{namespacePath}]: {ex.Message}", ex);
        }
        catch
        {
            // Logging must never break detection.
        }
    }

    /// <summary>
    /// Executes a WMI query and maps the live objects to <typeparamref name="T"/>.
    /// Every <see cref="ManagementObject"/> is disposed before return.
    /// Returns default on any failure; the selector itself never sees disposed objects.
    /// </summary>
    public static T? Query<T>(
        string query,
        Func<IReadOnlyList<ManagementObject>, T> selector,
        string namespacePath = @"root\cimv2"
    )
    {
        ManagementObject[] items = [];
        try
        {
            var scope = GetScope(namespacePath);
            using var searcher = new ManagementObjectSearcher(
                scope,
                new ObjectQuery(query),
                DefaultOptions
            );
            using var results = searcher.Get();
            items = results.Cast<ManagementObject>().ToArray();
            return selector(items);
        }
        catch (Exception ex)
        {
            ReportFailure(query, namespacePath, ex);
            return default;
        }
        finally
        {
            foreach (var item in items)
                item.Dispose();
        }
    }

    /// <summary>
    /// Maps the first row of a WMI query. The object is disposed before return.
    /// Returns default when the query yields nothing or fails.
    /// </summary>
    public static T QueryFirst<T>(
        string query,
        Func<ManagementObject, T> selector,
        string namespacePath = @"root\cimv2"
    )
    {
        ManagementObject[] items = [];
        try
        {
            var scope = GetScope(namespacePath);
            using var searcher = new ManagementObjectSearcher(
                scope,
                new ObjectQuery(query),
                DefaultOptions
            );
            using var results = searcher.Get();
            items = results.Cast<ManagementObject>().ToArray();
            if (items.Length == 0)
                return default!;
            return selector(items[0]);
        }
        catch (Exception ex)
        {
            ReportFailure(query, namespacePath, ex);
            return default!;
        }
        finally
        {
            foreach (var item in items)
                item.Dispose();
        }
    }

    /// <summary>Escapes a value for safe use in WQL string literals.</summary>
    public static string EscapeWql(string value)
    {
        return value.Replace("'", "''");
    }

    public static string? GetString(ManagementObject mo, string property)
    {
        try
        {
            return mo[property] switch
            {
                null => null,
                string[] arr => arr.Length > 0 ? arr[0].Trim() : null,
                var v => v.ToString()?.Trim() is { Length: > 0 } s ? s : null,
            };
        }
        catch
        {
            return null;
        }
    }

    public static int? GetInt(ManagementObject mo, string property)
    {
        try
        {
            var value = mo[property];
            return value is null ? null : Convert.ToInt32(value);
        }
        catch
        {
            return null;
        }
    }

    public static long? GetLong(ManagementObject mo, string property)
    {
        try
        {
            var value = mo[property];
            return value is null ? null : Convert.ToInt64(value);
        }
        catch
        {
            return null;
        }
    }

    public static bool? GetBool(ManagementObject mo, string property)
    {
        try
        {
            var value = mo[property];
            return value is null ? null : Convert.ToBoolean(value);
        }
        catch
        {
            return null;
        }
    }

    public static DateTime? GetWmiDate(ManagementObject mo, string property)
    {
        var raw = GetString(mo, property);
        if (string.IsNullOrEmpty(raw))
            return null;
        try
        {
            return ManagementDateTimeConverter.ToDateTime(raw);
        }
        catch
        {
            return null;
        }
    }
}

// ============================================================================
// NATIVE APIS (no process spawning)
// ============================================================================

internal static class NativeMemory
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    /// <summary>Same API Task Manager uses for memory usage.</summary>
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

[SupportedOSPlatform("windows")]
internal static class NativeFirmware
{
    // FIRMWARE_TYPE: 0 Unknown, 1 Bios, 2 Uefi.
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFirmwareType(out uint firmwareType);

    public static FirmwareMode GetMode()
    {
        try
        {
            if (!GetFirmwareType(out var type))
                return FirmwareMode.Unknown;
            return type switch
            {
                1 => FirmwareMode.Legacy,
                2 => FirmwareMode.Uefi,
                _ => FirmwareMode.Unknown,
            };
        }
        catch
        {
            return FirmwareMode.Unknown;
        }
    }
}

internal static class PowerReader
{
    private static PowerPlanService? _service;

    internal static void Configure(PowerPlanService service)
    {
        _service = service;
    }

    internal static (Guid? Id, string? Name) ReadActive()
    {
        var service = _service;
        if (service is null)
            return (null, null);
        try
        {
            var id = service.GetActiveSchemeId();
            if (id is null)
                return (null, null);
            return (id.Value, service.GetSchemeName(id.Value));
        }
        catch
        {
            return (null, null);
        }
    }
}

internal static class CpuProvider
{
    private static readonly Lazy<CpuInfo> _cached = new(Load, LazyThreadSafetyMode.PublicationOnly);

    /// <summary>Registry (fast) + one targeted WMI row for caches/virtualization.</summary>
    public static CpuInfo Get()
    {
        return _cached.Value;
    }

    private static CpuInfo Load()
    {
        try
        {
            string? name = null;
            string? manufacturer = null;
            int? currentMHz = null;

            using (
                var cpuKey = Registry.LocalMachine.OpenSubKey(
                    @"HARDWARE\DESCRIPTION\System\CentralProcessor\0"
                )
            )
            {
                if (cpuKey != null)
                {
                    name = cpuKey.GetValue("ProcessorNameString")?.ToString()?.Trim();
                    manufacturer = cpuKey.GetValue("VendorIdentifier")?.ToString()?.Trim();
                    if (cpuKey.GetValue("~MHz") is int mhz)
                        currentMHz = mhz;
                }
            }

            var threads = Environment.ProcessorCount;
            int? cores = null;
            int? maxMHz = null;
            int? l2kb = null;
            int? l3kb = null;
            bool? virtualization = null;

            var row = WmiHelper.QueryFirst(
                "SELECT NumberOfCores, MaxClockSpeed, L2CacheSize, L3CacheSize, VirtualizationFirmwareEnabled FROM Win32_Processor",
                static mo =>
                    (
                        Cores: WmiHelper.GetInt(mo, "NumberOfCores"),
                        MaxMHz: WmiHelper.GetInt(mo, "MaxClockSpeed"),
                        L2: WmiHelper.GetInt(mo, "L2CacheSize"),
                        L3: WmiHelper.GetInt(mo, "L3CacheSize"),
                        Virt: WmiHelper.GetBool(mo, "VirtualizationFirmwareEnabled")
                    )
            );
            if (row != default)
            {
                cores = row.Cores;
                maxMHz = row.MaxMHz;
                l2kb = row.L2;
                l3kb = row.L3;
                virtualization = row.Virt;
            }

            return new CpuInfo
            {
                Name = string.IsNullOrWhiteSpace(name) ? null : name,
                Vendor = DetectVendor(manufacturer),
                Architecture = MapArchitecture(),
                CoreCount = cores is > 0 ? cores.Value : threads,
                ThreadCount = threads,
                MaxFrequencyMhz = maxMHz ?? currentMHz,
                CurrentFrequencyMhz = currentMHz,
                L2CacheKB = l2kb,
                L3CacheKB = l3kb,
                VirtualizationFirmwareEnabled = virtualization,
            };
        }
        catch
        {
            return CpuInfo.Unknown;
        }
    }

    internal static Architecture MapArchitecture()
    {
        return RuntimeInformation.OSArchitecture switch
        {
            DotNetArch.X64 => Architecture.X64,
            DotNetArch.X86 => Architecture.X86,
            DotNetArch.Arm64 => Architecture.Arm64,
            _ => Architecture.Unknown,
        };
    }

    internal static CpuVendor DetectVendor(string? manufacturer)
    {
        if (string.IsNullOrWhiteSpace(manufacturer))
            return CpuVendor.Unknown;
        var lower = manufacturer.ToLowerInvariant();
        if (lower.Contains("intel") || lower.Contains("genuineintel"))
            return CpuVendor.Intel;
        if (lower.Contains("amd") || lower.Contains("authenticamd"))
            return CpuVendor.Amd;
        return CpuVendor.Unknown;
    }
}

internal static class MemoryProvider
{
    private static readonly Lazy<IReadOnlyList<MemoryModuleInfo>> _cachedModules = new(
        LoadPhysicalModules,
        LazyThreadSafetyMode.PublicationOnly
    );

    /// <summary>Live usage via GlobalMemoryStatusEx + cached module details.</summary>
    public static MemoryInfo Get()
    {
        try
        {
            // Totals first: empty/failed module enumeration never zeroes live totals.
            var status = NativeMemory.GetMemoryStatus();
            var modules = _cachedModules.Value;
            if (status is not { } s || s.ullTotalPhys == 0)
                return FallbackFromModules(modules);

            return new MemoryInfo
            {
                TotalBytes = (long)s.ullTotalPhys,
                AvailableBytes = (long)s.ullAvailPhys,
                SpeedMTps = MaxSpeed(modules),
                Type = DominantType(modules),
                OverclockProfile = MemoryProfileState.Unknown,
                Modules = modules,
            };
        }
        catch
        {
            return MemoryInfo.Unknown;
        }
    }

    private static MemoryInfo FallbackFromModules(IReadOnlyList<MemoryModuleInfo> modules)
    {
        // GlobalMemoryStatusEx failed: report installed capacity, zero live data.
        long total = 0;
        foreach (var m in modules)
            total +=
                m.CapacityBytes > 0 ? m.CapacityBytes : (long)(m.CapacityGB * 1024 * 1024 * 1024);
        return new MemoryInfo
        {
            TotalBytes = total,
            AvailableBytes = 0,
            SpeedMTps = null,
            Type = DominantType(modules),
            OverclockProfile = MemoryProfileState.Unknown,
            Modules = modules,
        };
    }

    internal static int? MaxSpeed(IReadOnlyList<MemoryModuleInfo> modules)
    {
        int? speed = null;
        foreach (var m in modules)
            if (m.SpeedMTps is > 0 && (speed is null || m.SpeedMTps > speed))
                speed = m.SpeedMTps;
        return speed;
    }

    internal static MemoryType DominantType(IReadOnlyList<MemoryModuleInfo> modules)
    {
        var counts = new Dictionary<MemoryType, int>();
        foreach (var m in modules)
        {
            if (m.Type == MemoryType.Unknown)
                continue;
            counts.TryGetValue(m.Type, out var n);
            counts[m.Type] = n + 1;
        }
        var best = MemoryType.Unknown;
        var bestCount = 0;
        foreach (var (type, n) in counts)
            if (n > bestCount)
            {
                best = type;
                bestCount = n;
            }
        return best;
    }

    private static IReadOnlyList<MemoryModuleInfo> LoadPhysicalModules()
    {
        // Null = WMI failed: throw so the Lazy retries on next access.
        // Empty = soldered-in RAM with no rows: valid, cached as-is.
        return WmiHelper.Query(
                "SELECT Capacity, Speed, ConfiguredClockSpeed, SMBIOSMemoryType, Manufacturer, PartNumber, DeviceLocator FROM Win32_PhysicalMemory",
                static items =>
                {
                    var list = new List<MemoryModuleInfo>(items.Count);
                    foreach (var mem in items)
                    {
                        var capacityBytes = WmiHelper.GetLong(mem, "Capacity") ?? 0;
                        if (capacityBytes <= 0)
                            continue;
                        var configured = WmiHelper.GetInt(mem, "ConfiguredClockSpeed");
                        var memType = MapMemoryType(WmiHelper.GetInt(mem, "SMBIOSMemoryType"));
                        var speed = WmiHelper.GetInt(mem, "Speed");
                        list.Add(
                            new MemoryModuleInfo
                            {
                                CapacityGB = Math.Round(
                                    capacityBytes / (1024.0 * 1024.0 * 1024.0),
                                    2
                                ),
                                CapacityBytes = capacityBytes,
                                SpeedMTps =
                                    configured is > 0 ? configured
                                    : speed is > 0 ? speed
                                    : null,
                                Type = memType,
                                Manufacturer = WmiHelper.GetString(mem, "Manufacturer"),
                                PartNumber = WmiHelper.GetString(mem, "PartNumber"),
                                Slot = WmiHelper.GetString(mem, "DeviceLocator"),
                            }
                        );
                    }
                    return list;
                }
            ) ?? throw new InvalidOperationException("Win32_PhysicalMemory query failed.");
    }

    /// <summary>SMBIOS memory-type codes (DMTF DSP0134). Unspecified = Unknown, unlisted = Other, never guessed.</summary>
    internal static MemoryType MapMemoryType(int? code)
    {
        return code switch
        {
            0x12 => MemoryType.Ddr,
            0x13 or 0x14 or 0x19 => MemoryType.Ddr2,
            0x18 => MemoryType.Ddr3,
            0x1A => MemoryType.Ddr4,
            0x22 => MemoryType.Ddr5,
            0x1B or 0x1C or 0x1D or 0x1E or 0x23 => MemoryType.Lpddr,
            0x20 or 0x21 or 0x24 => MemoryType.Hbm,
            0x0F or 0x10 or 0x11 => MemoryType.Sdram,
            null or 0x00 or 0x02 => MemoryType.Unknown,
            _ => MemoryType.Other,
        };
    }
}

internal static class DiskMediaDetector
{
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
    private const uint GENERIC_READ = 0x0;
    private const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x2D1400;

    private static readonly ConcurrentDictionary<string, StorageMediaType> MediaTypeCache = new(
        StringComparer.OrdinalIgnoreCase
    );

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
    [return: MarshalAs(UnmanagedType.Bool)]
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

    /// <summary>Seek-penalty ioctl (fast, cached). Unknown when the handle/ioctl fails.</summary>
    public static StorageMediaType Detect(string driveLetter)
    {
        var key = driveLetter.TrimEnd('\\', '/').ToUpperInvariant();
        return MediaTypeCache.GetOrAdd(key, static lk => DetectViaSeekPenalty(lk));
    }

    private static StorageMediaType DetectViaSeekPenalty(string driveLetter)
    {
        try
        {
            var trimmed = driveLetter.TrimEnd('\\', '/');
            if (string.IsNullOrWhiteSpace(trimmed))
                return StorageMediaType.Unknown;

            using var handle = CreateFile(
                @"\\.\" + trimmed,
                GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero
            );
            if (handle.IsInvalid)
                return StorageMediaType.Unknown;

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
                var ok = DeviceIoControl(
                    handle,
                    IOCTL_STORAGE_QUERY_PROPERTY,
                    queryPtr,
                    (uint)querySize,
                    resultPtr,
                    (uint)resultSize,
                    out _,
                    IntPtr.Zero
                );
                if (!ok)
                    return StorageMediaType.Unknown;
                var desc = Marshal.PtrToStructure<DEVICE_SEEK_PENALTY_DESCRIPTOR>(resultPtr);
                return desc.IncursSeekPenalty ? StorageMediaType.Hdd : StorageMediaType.Ssd;
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
            return StorageMediaType.Unknown;
        }
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
    private sealed record PhysicalDiskInfo(
        string DeviceID,
        int DiskNumber,
        string? Model,
        string? SerialNumber,
        StorageMediaType MediaType
    );

    // Physical topology changes ~never at runtime: cache successes, retry failures.
    // The fast (2s-tick) path only reads these fields and never triggers a load.
    private static readonly object _cacheLock = new();
    private static IReadOnlyList<PhysicalDiskInfo>? _cachedDisks;
    private static IReadOnlyDictionary<string, PhysicalDiskInfo>? _cachedDriveMap;
    private static readonly IReadOnlyDictionary<string, PhysicalDiskInfo> EmptyMap = new Dictionary<
        string,
        PhysicalDiskInfo
    >(StringComparer.OrdinalIgnoreCase);

    /// <summary>Full scan (first load / manual refresh): WMI topology + live sizes.</summary>
    public static StorageInfo GetFull()
    {
        return BuildVolumes(resolveUnmapped: true);
    }

    /// <summary>
    /// Live 2s-tick path: DriveInfo sizes + cached topology only. Never issues a new
    /// WMI query, so the Dashboard timer cannot cause a WMI storm.
    /// </summary>
    public static StorageInfo GetFast()
    {
        return BuildVolumes(resolveUnmapped: false);
    }

    private static IReadOnlyList<PhysicalDiskInfo> GetOrLoadDisks()
    {
        if (_cachedDisks is { Count: > 0 } cached)
            return cached;
        lock (_cacheLock)
        {
            if (_cachedDisks is { Count: > 0 } fresh)
                return fresh;
            // Empty/failed loads are served once but never pinned: next full scan retries.
            var loaded = LoadPhysicalDisks();
            if (loaded.Count > 0)
                _cachedDisks = loaded;
            return loaded;
        }
    }

    private static IReadOnlyDictionary<string, PhysicalDiskInfo> GetOrLoadDriveMap(
        IReadOnlyList<PhysicalDiskInfo> disks
    )
    {
        if (_cachedDriveMap is { Count: > 0 } cached)
            return cached;
        lock (_cacheLock)
        {
            if (_cachedDriveMap is { Count: > 0 } fresh)
                return fresh;
            var built = BuildDriveToDiskMap(disks);
            if (built.Count > 0)
                _cachedDriveMap = built;
            return built;
        }
    }

    private static StorageInfo BuildVolumes(bool resolveUnmapped)
    {
        try
        {
            // Fast path: cached topology only, zero new WMI queries.
            var disks = resolveUnmapped ? GetOrLoadDisks() : _cachedDisks ?? [];
            var driveMap = resolveUnmapped ? GetOrLoadDriveMap(disks) : _cachedDriveMap ?? EmptyMap;
            var volumes = new List<StorageVolume>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                StorageVolume? volume;
                try
                {
                    if (!drive.IsReady)
                        continue;
                    volume = BuildVolume(drive, disks, driveMap, resolveUnmapped);
                }
                catch
                {
                    // One transient drive never fails the whole scan.
                    continue;
                }
                if (volume is not null)
                    volumes.Add(volume);
            }
            return new StorageInfo { Volumes = volumes };
        }
        catch
        {
            return StorageInfo.Unknown;
        }
    }

    private static StorageVolume BuildVolume(
        DriveInfo drive,
        IReadOnlyList<PhysicalDiskInfo> disks,
        IReadOnlyDictionary<string, PhysicalDiskInfo> driveMap,
        bool resolveUnmapped
    )
    {
        var letter = drive.Name.TrimEnd('\\');
        var key = letter.TrimEnd(':');
        driveMap.TryGetValue(key, out var disk);

        if (disk is null && resolveUnmapped)
            disk = ResolveViaAssociators(disks, letter);

        var media = disk?.MediaType ?? StorageMediaType.Unknown;
        if (media == StorageMediaType.Unknown && disk?.Model is { } model)
            media = InferFromModel(model);
        if (media == StorageMediaType.Unknown)
        {
            // Seek-penalty ioctl splits SSD/HDD reliably; NVMe comes only
            // from the topology/model path above, never from here.
            var probed = DiskMediaDetector.Detect(drive.Name);
            if (probed != StorageMediaType.Unknown)
                media = probed;
        }

        return new StorageVolume
        {
            DriveLetter = letter,
            IsSystemDrive = DiskMediaDetector.IsSystemDrive(drive.Name),
            Label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel,
            FileSystem = string.IsNullOrWhiteSpace(drive.DriveFormat) ? null : drive.DriveFormat,
            TotalBytes = drive.TotalSize,
            FreeBytes = drive.AvailableFreeSpace,
            MediaType = media,
            Model = disk?.Model,
            IsRemovable = drive.DriveType == DriveType.Removable,
        };
    }

    internal static StorageMediaType InferFromModel(string model)
    {
        var lower = model.ToLowerInvariant();
        if (lower.Contains("nvme"))
            return StorageMediaType.Nvme;
        if (lower.Contains("ssd") || lower.Contains("solid state"))
            return StorageMediaType.Ssd;
        if (lower.Contains("hdd") || lower.Contains("hard disk"))
            return StorageMediaType.Hdd;
        return StorageMediaType.Unknown;
    }

    /// <summary>
    /// Win32_DiskDrive string fields: trusted for NVMe/SSD keywords only, never HDD.
    /// A wrong vendor value can only upgrade Unknown, never override topology.
    /// </summary>
    internal static StorageMediaType MapDriveMedia(string? mediaType, string? interfaceType)
    {
        var combined = $"{mediaType} {interfaceType}".ToLowerInvariant();
        if (combined.Contains("nvme"))
            return StorageMediaType.Nvme;
        if (combined.Contains("ssd") || combined.Contains("solid state"))
            return StorageMediaType.Ssd;
        return StorageMediaType.Unknown;
    }

    private static IReadOnlyList<PhysicalDiskInfo> LoadPhysicalDisks()
    {
        var disks = LoadViaMsftPhysicalDisk();
        return disks.Count > 0 ? disks : LoadViaWin32DiskDrive();
    }

    private static List<PhysicalDiskInfo> LoadViaMsftPhysicalDisk()
    {
        try
        {
            return WmiHelper.Query(
                    "SELECT DeviceId, FriendlyName, SerialNumber, MediaType, BusType FROM MSFT_PhysicalDisk",
                    static items =>
                    {
                        var list = new List<PhysicalDiskInfo>(items.Count);
                        foreach (var disk in items)
                        {
                            var mediaValue = WmiHelper.GetInt(disk, "MediaType") ?? 0;
                            var busValue = WmiHelper.GetInt(disk, "BusType") ?? 0;
                            var media = mediaValue switch
                            {
                                // MSFT_PhysicalDisk MediaType: 3 HDD, 4 SSD, 5 SCM.
                                3 => StorageMediaType.Hdd,
                                4 => busValue == 17 ? StorageMediaType.Nvme : StorageMediaType.Ssd,
                                _ => StorageMediaType.Unknown,
                            };
                            var deviceId = NullIfEmpty(WmiHelper.GetString(disk, "DeviceId")) ?? "";
                            list.Add(
                                new PhysicalDiskInfo(
                                    deviceId,
                                    TryParseDiskNumber(deviceId),
                                    NullIfEmpty(WmiHelper.GetString(disk, "FriendlyName")),
                                    NullIfEmpty(WmiHelper.GetString(disk, "SerialNumber"))?.Trim(),
                                    media
                                )
                            );
                        }
                        return list;
                    },
                    @"root\Microsoft\Windows\Storage"
                ) ?? new List<PhysicalDiskInfo>();
        }
        catch
        {
            return new List<PhysicalDiskInfo>();
        }
    }

    private static List<PhysicalDiskInfo> LoadViaWin32DiskDrive()
    {
        try
        {
            return WmiHelper.Query(
                    "SELECT DeviceID, Model, Index, MediaType, InterfaceType FROM Win32_DiskDrive",
                    static items =>
                    {
                        var list = new List<PhysicalDiskInfo>(items.Count);
                        foreach (var disk in items)
                        {
                            var model = NullIfEmpty(WmiHelper.GetString(disk, "Model"));
                            var media = MapDriveMedia(
                                WmiHelper.GetString(disk, "MediaType"),
                                WmiHelper.GetString(disk, "InterfaceType")
                            );
                            list.Add(
                                new PhysicalDiskInfo(
                                    WmiHelper.GetString(disk, "DeviceID") ?? "",
                                    WmiHelper.GetInt(disk, "Index") ?? -1,
                                    model,
                                    null,
                                    media != StorageMediaType.Unknown ? media
                                        : model is not null ? InferFromModel(model)
                                        : StorageMediaType.Unknown
                                )
                            );
                        }
                        return list;
                    }
                ) ?? new List<PhysicalDiskInfo>();
        }
        catch
        {
            return new List<PhysicalDiskInfo>();
        }
    }

    private static IReadOnlyDictionary<string, PhysicalDiskInfo> BuildDriveToDiskMap(
        IReadOnlyList<PhysicalDiskInfo> disks
    )
    {
        var map = new Dictionary<string, PhysicalDiskInfo>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var byNumber = disks
                .Where(static d => d.DiskNumber >= 0)
                .ToDictionary(static d => d.DiskNumber);
            if (byNumber.Count == 0)
                return map;

            var rows =
                WmiHelper.Query(
                    "SELECT DriveLetter, DiskNumber FROM MSFT_Partition WHERE DriveLetter IS NOT NULL",
                    static items =>
                    {
                        var list = new List<(string Letter, int Disk)>(items.Count);
                        foreach (var p in items)
                        {
                            var letter = WmiHelper.GetString(p, "DriveLetter");
                            var number = WmiHelper.GetInt(p, "DiskNumber");
                            if (!string.IsNullOrEmpty(letter) && number.HasValue)
                                list.Add((letter!, number.Value));
                        }
                        return list;
                    },
                    @"root\Microsoft\Windows\Storage"
                ) ?? new List<(string, int)>();

            foreach (var (letter, number) in rows)
                if (byNumber.TryGetValue(number, out var info))
                    map.TryAdd(letter.TrimEnd(':'), info);
        }
        catch
        {
            // Storage namespace missing on older OS: callers fall back per-drive.
        }
        return map;
    }

    /// <summary>Slow per-drive ASSOCIATORS fallback, used only by full scans.</summary>
    private static PhysicalDiskInfo? ResolveViaAssociators(
        IReadOnlyList<PhysicalDiskInfo> disks,
        string letter
    )
    {
        try
        {
            var byNumber = disks
                .Where(static d => d.DiskNumber >= 0)
                .ToDictionary(static d => d.DiskNumber);
            var partitions = WmiHelper.Query(
                $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{WmiHelper.EscapeWql(letter)}'}} WHERE AssocClass=Win32_LogicalDiskToPartition",
                static items =>
                {
                    var list = new List<string>(items.Count);
                    foreach (var p in items)
                        if (WmiHelper.GetString(p, "DeviceID") is { } id)
                            list.Add(id);
                    return list;
                }
            );
            foreach (var partitionId in partitions ?? new List<string>())
            {
                var indexes = WmiHelper.Query(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{WmiHelper.EscapeWql(partitionId)}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition",
                    static items =>
                    {
                        var list = new List<(int Index, string Device)>(items.Count);
                        foreach (var d in items)
                            list.Add(
                                (
                                    WmiHelper.GetInt(d, "Index") ?? -1,
                                    WmiHelper.GetString(d, "DeviceID") ?? ""
                                )
                            );
                        return list;
                    }
                );
                foreach (var (index, device) in indexes ?? new List<(int, string)>())
                {
                    if (index >= 0 && byNumber.TryGetValue(index, out var info))
                        return info;
                    foreach (var entry in disks)
                        if (
                            !string.IsNullOrEmpty(entry.DeviceID)
                            && string.Equals(
                                entry.DeviceID,
                                device,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                            return entry;
                }
            }
        }
        catch
        {
            // Ignore: media falls back to probe/Unknown.
        }

        if (disks.Count == 1)
            return disks[0];
        return null;
    }

    private static int TryParseDiskNumber(string? text)
    {
        return int.TryParse(text, out var n) ? n : -1;
    }

    private static string? NullIfEmpty(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}

// ============================================================================
// DXGI INTEROP (COM P/Invoke, unchanged behavior)
// ============================================================================

internal static class DxgiHelper
{
    public const uint DXGI_ADAPTER_FLAG_SOFTWARE = 2;

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
        return desc.VendorId == MICROSOFT_VENDOR_ID && desc.DeviceId == BASIC_RENDER_DEVICE_ID;
    }

    [ComImport]
    [Guid("770aae78-f26f-4dba-a829-253c83d1b387")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDXGIFactory1
    {
        void SetPrivateData([In] ref Guid Name, uint DataSize, IntPtr pData);
        void SetPrivateDataInterface(
            [In] ref Guid Name,
            [MarshalAs(UnmanagedType.IUnknown)] object pUnknown
        );
        void GetPrivateData([In] ref Guid Name, ref uint pDataSize, IntPtr pData);
        void GetParent([In] ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppParent);
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

        [PreserveSig]
        int EnumAdapters1(uint Adapter, out IDXGIAdapter1? ppAdapter);
        bool IsCurrent();
    }

    [ComImport]
    [Guid("29038f61-3839-4626-91fd-086879011a05")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDXGIAdapter1
    {
        void SetPrivateData([In] ref Guid Name, uint DataSize, IntPtr pData);
        void SetPrivateDataInterface(
            [In] ref Guid Name,
            [MarshalAs(UnmanagedType.IUnknown)] object pUnknown
        );
        void GetPrivateData([In] ref Guid Name, ref uint pDataSize, IntPtr pData);
        void GetParent([In] ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppParent);
        void EnumOutputs(uint Output, [MarshalAs(UnmanagedType.IUnknown)] out object ppOutput);
        void GetDesc(out DXGI_ADAPTER_DESC pDesc);
        int CheckInterfaceSupport([In] ref Guid InterfaceName, out long pUMDVersion);
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
        public uint Flags;
    }
}

// ============================================================================
// GPU PROVIDER (DXGI + one WMI pass for driver details)
// ============================================================================

internal static class GpuProvider
{
    /// <summary>Physical GPUs ordered by DXGI index; empty when nothing usable found.</summary>
    public static IReadOnlyList<GpuInfo> GetAll()
    {
        try
        {
            return GetAllViaDxgi();
        }
        catch
        {
            return GetAllViaWmi();
        }
    }

    /// <summary>
    /// Primary = most VRAM, then vendor priority (Nvidia &gt; Amd &gt; Intel).
    /// On hybrid laptops DXGI index 0 is often the iGPU, so index order misleads.
    /// </summary>
    public static GpuInfo? GetPrimary(IReadOnlyList<GpuInfo> gpus)
    {
        if (gpus.Count == 0)
            return null;
        if (gpus.Count == 1)
            return gpus[0];

        GpuInfo? best = null;
        foreach (var g in gpus)
        {
            if (best is null)
            {
                best = g;
                continue;
            }
            var memG = g.VramMB ?? 0;
            var memBest = best.VramMB ?? 0;
            if (memG != memBest)
            {
                if (memG > memBest)
                    best = g;
                continue;
            }
            if (VendorScore(g.Vendor) > VendorScore(best.Vendor))
                best = g;
        }
        return best;
    }

    internal static int VendorScore(GpuVendor vendor)
    {
        return vendor switch
        {
            GpuVendor.Nvidia => 2,
            GpuVendor.Amd => 1,
            _ => 0,
        };
    }

    private static IReadOnlyList<GpuInfo> GetAllViaDxgi()
    {
        DxgiHelper.IDXGIFactory1? factory = null;
        try
        {
            factory = DxgiHelper.CreateFactory();
            var wmiLookup = BuildWmiLookup();
            var gpus = new List<GpuInfo>();
            for (uint i = 0; ; i++)
            {
                var hr = factory.EnumAdapters1(i, out var adapter);
                if (hr != 0 || adapter == null)
                    break;
                try
                {
                    adapter.GetDesc1(out var desc);
                    if (DxgiHelper.IsSoftwareAdapter(desc))
                        continue;
                    var name = desc.Description?.Trim();
                    if (string.IsNullOrWhiteSpace(name) || IsVirtualAdapter(name))
                        continue;
                    var memoryMB = (int)((long)desc.DedicatedVideoMemory / (1024 * 1024));
                    var match = FindWmiMatch(name, desc.VendorId, desc.DeviceId, wmiLookup);
                    gpus.Add(
                        new GpuInfo
                        {
                            Name = name,
                            Vendor = DetectVendorById(desc.VendorId),
                            VramMB = memoryMB > 0 ? memoryMB : null,
                            DriverVersion = match?.DriverVersion,
                            DriverDate = match?.DriverDate,
                            DeviceId = match?.DeviceId,
                            PnpDeviceId = match?.PnpDeviceId,
                        }
                    );
                }
                finally
                {
                    Marshal.ReleaseComObject(adapter);
                }
            }
            return gpus;
        }
        finally
        {
            if (factory != null)
                Marshal.ReleaseComObject(factory);
        }
    }

    private static IReadOnlyList<GpuInfo> GetAllViaWmi()
    {
        try
        {
            return WmiHelper.Query(
                    "SELECT Name, DriverVersion, DriverDate, DeviceID, PNPDeviceID, AdapterRAM FROM Win32_VideoController",
                    static items =>
                    {
                        var list = new List<GpuInfo>(items.Count);
                        foreach (var c in items)
                        {
                            var name = WmiHelper.GetString(c, "Name");
                            if (string.IsNullOrWhiteSpace(name) || IsVirtualAdapter(name!))
                                continue;
                            var adapterRam = WmiHelper.GetLong(c, "AdapterRAM");
                            var pnpId = WmiHelper.GetString(c, "PNPDeviceID");
                            list.Add(
                                new GpuInfo
                                {
                                    Name = name,
                                    Vendor = DetectVendor(name!, pnpId),
                                    VramMB = adapterRam is > 0
                                        ? (int)(adapterRam.Value / (1024 * 1024))
                                        : null,
                                    DriverVersion = WmiHelper.GetString(c, "DriverVersion"),
                                    DriverDate = WmiHelper.GetWmiDate(c, "DriverDate"),
                                    DeviceId = WmiHelper.GetString(c, "DeviceID"),
                                    PnpDeviceId = pnpId,
                                }
                            );
                        }
                        return (IReadOnlyList<GpuInfo>)list;
                    }
                ) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static List<WmiGpuEntry> BuildWmiLookup()
    {
        try
        {
            return WmiHelper.Query(
                    "SELECT Name, DriverVersion, DriverDate, DeviceID, PNPDeviceID FROM Win32_VideoController",
                    static items =>
                    {
                        var list = new List<WmiGpuEntry>(items.Count);
                        foreach (var c in items)
                        {
                            var pnpId = WmiHelper.GetString(c, "PNPDeviceID");
                            ParsePnpIds(pnpId, out var vendorId, out var deviceId);
                            list.Add(
                                new WmiGpuEntry(
                                    WmiHelper.GetString(c, "Name"),
                                    WmiHelper.GetString(c, "DriverVersion"),
                                    WmiHelper.GetWmiDate(c, "DriverDate"),
                                    WmiHelper.GetString(c, "DeviceID"),
                                    pnpId,
                                    vendorId,
                                    deviceId
                                )
                            );
                        }
                        return list;
                    }
                ) ?? new List<WmiGpuEntry>();
        }
        catch
        {
            return new List<WmiGpuEntry>();
        }
    }

    private static void ParsePnpIds(string? pnpId, out uint vendorId, out uint deviceId)
    {
        vendorId = 0;
        deviceId = 0;
        if (string.IsNullOrWhiteSpace(pnpId))
            return;
        var upper = pnpId.ToUpperInvariant();
        var venIdx = upper.IndexOf("VEN_", StringComparison.Ordinal);
        if (venIdx >= 0 && venIdx + 8 <= upper.Length)
            uint.TryParse(
                upper.Substring(venIdx + 4, 4),
                NumberStyles.HexNumber,
                null,
                out vendorId
            );
        var devIdx = upper.IndexOf("DEV_", StringComparison.Ordinal);
        if (devIdx >= 0 && devIdx + 8 <= upper.Length)
            uint.TryParse(
                upper.Substring(devIdx + 4, 4),
                NumberStyles.HexNumber,
                null,
                out deviceId
            );
    }

    private static WmiGpuEntry? FindWmiMatch(
        string dxgiName,
        uint dxgiVendorId,
        uint dxgiDeviceId,
        List<WmiGpuEntry> entries
    )
    {
        foreach (var e in entries)
            if (e.VendorId == dxgiVendorId && e.HardwareDeviceId == dxgiDeviceId)
                return e;
        var lower = dxgiName.ToLowerInvariant();
        foreach (var e in entries)
            if (!string.IsNullOrEmpty(e.Name) && lower.Contains(e.Name.ToLowerInvariant()))
                return e;
        return null;
    }

    private static bool IsVirtualAdapter(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.Contains("microsoft basic")
            || lower.Contains("remote desktop")
            || lower.Contains("virtual");
    }

    internal static GpuVendor DetectVendorById(uint vendorId)
    {
        return vendorId switch
        {
            0x10DE => GpuVendor.Nvidia,
            0x1002 => GpuVendor.Amd,
            0x8086 => GpuVendor.Intel,
            _ => GpuVendor.Unknown,
        };
    }

    internal static GpuVendor DetectVendor(string name, string? pnpId)
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
            return GpuVendor.Nvidia;
        if (
            nameLower.Contains("amd")
            || nameLower.Contains("radeon")
            || pnpLower.Contains("ven_1002")
        )
            return GpuVendor.Amd;
        if (
            nameLower.Contains("intel")
            || nameLower.Contains("iris")
            || nameLower.Contains("uhd")
            || pnpLower.Contains("ven_8086")
        )
            return GpuVendor.Intel;
        return GpuVendor.Unknown;
    }

    private sealed record WmiGpuEntry(
        string? Name,
        string? DriverVersion,
        DateTime? DriverDate,
        string? DeviceId,
        string? PnpDeviceId,
        uint VendorId,
        uint HardwareDeviceId
    );
}

// ============================================================================
// WINDOWS / FIRMWARE / SECURITY / POWER / RUNTIME PROVIDERS
// ============================================================================

internal static class WindowsProvider
{
    private static readonly Lazy<WindowsInfo> _cached = new(
        Load,
        LazyThreadSafetyMode.PublicationOnly
    );

    public static WindowsInfo Get()
    {
        return _cached.Value;
    }

    private static WindowsInfo Load()
    {
        try
        {
            int? build = null;
            string? displayVersion = null;
            var edition = WindowsEdition.Unknown;
            DateTime? installDate = null;

            using (
                var ntKey = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion"
                )
            )
            {
                if (ntKey != null)
                {
                    var buildText =
                        ntKey.GetValue("CurrentBuildNumber")?.ToString()
                        ?? ntKey.GetValue("CurrentBuild")?.ToString();
                    if (int.TryParse(buildText?.Split('.')[0], out var parsed))
                        build = parsed;
                    displayVersion = NullIfEmpty(ntKey.GetValue("DisplayVersion")?.ToString());
                    edition = MapEdition(ntKey.GetValue("EditionID")?.ToString());
                    installDate = ParseInstallDate(ntKey.GetValue("InstallDate"));
                }
            }

            var lastBoot = WmiHelper.QueryFirst(
                "SELECT LastBootUpTime FROM Win32_OperatingSystem",
                static mo => WmiHelper.GetWmiDate(mo, "LastBootUpTime")
            );

            return new WindowsInfo
            {
                BuildNumber = build,
                DisplayVersion = displayVersion,
                Edition = edition,
                Architecture = CpuProvider.MapArchitecture(),
                DeviceKind = DetectDeviceKind(),
                InstallDate = installDate,
                LastBootTime = lastBoot,
            };
        }
        catch
        {
            return WindowsInfo.Unknown;
        }
    }

    internal static WindowsEdition MapEdition(string? editionId)
    {
        if (string.IsNullOrWhiteSpace(editionId))
            return WindowsEdition.Unknown;
        var id = editionId.Trim();
        if (id.StartsWith("Server", StringComparison.OrdinalIgnoreCase))
            return WindowsEdition.Server;
        if (id.StartsWith("Core", StringComparison.OrdinalIgnoreCase))
            return WindowsEdition.Home;
        return id switch
        {
            "Home" => WindowsEdition.Home,
            "Professional" => WindowsEdition.Pro,
            "ProfessionalEducation" => WindowsEdition.Education,
            "Education" => WindowsEdition.Education,
            "Enterprise" => WindowsEdition.Enterprise,
            _ when id.Contains("Education", StringComparison.OrdinalIgnoreCase) =>
                WindowsEdition.Education,
            _ when id.Contains("Enterprise", StringComparison.OrdinalIgnoreCase) =>
                WindowsEdition.Enterprise,
            _ when id.Contains("Pro", StringComparison.OrdinalIgnoreCase) => WindowsEdition.Pro,
            _ => WindowsEdition.Unknown,
        };
    }

    private static DateTime? ParseInstallDate(object? value)
    {
        try
        {
            long seconds = value switch
            {
                int i => i,
                uint u => u,
                long l => l,
                _ => 0,
            };
            if (seconds <= 0)
                return null;
            return DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime;
        }
        catch
        {
            return null;
        }
    }

    private static DeviceKind DetectDeviceKind()
    {
        try
        {
            return WmiHelper.QueryFirst(
                "SELECT ChassisTypes FROM Win32_SystemEnclosure",
                static mo =>
                    mo["ChassisTypes"] is ushort[] { Length: > 0 } types
                        ? MapChassisType(types)
                        : DeviceKind.Unknown
            );
        }
        catch
        {
            return DeviceKind.Unknown;
        }
    }

    internal static DeviceKind MapChassisType(IEnumerable<ushort> types)
    {
        foreach (var type in types)
            switch (type)
            {
                case 8 or 9 or 10 or 11 or 14 or 30 or 31 or 32:
                    return DeviceKind.Laptop;
                case >= 1 and <= 7 or 12 or 13 or >= 15 and <= 29 or >= 33 and <= 36:
                    return DeviceKind.Desktop;
            }
        return DeviceKind.Unknown;
    }

    private static string? NullIfEmpty(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}

internal static class FirmwareProvider
{
    private static readonly Lazy<FirmwareInfo> _cached = new(
        Load,
        LazyThreadSafetyMode.PublicationOnly
    );

    public static FirmwareInfo Get()
    {
        return _cached.Value;
    }

    private static FirmwareInfo Load()
    {
        try
        {
            var bios = WmiHelper.QueryFirst(
                "SELECT Manufacturer, BIOSVersion, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS",
                static mo => new
                {
                    Manufacturer = WmiHelper.GetString(mo, "Manufacturer"),
                    Version = WmiHelper.GetString(mo, "BIOSVersion"),
                    Smbios = WmiHelper.GetString(mo, "SMBIOSBIOSVersion"),
                    Date = WmiHelper.GetWmiDate(mo, "ReleaseDate"),
                }
            );
            var board = WmiHelper.QueryFirst(
                "SELECT Manufacturer, Product FROM Win32_BaseBoard",
                static mo => new
                {
                    Manufacturer = WmiHelper.GetString(mo, "Manufacturer"),
                    Product = WmiHelper.GetString(mo, "Product"),
                }
            );

            string? motherboard = null;
            if (board is not null)
            {
                var maker = board.Manufacturer?.Trim();
                var product = board.Product?.Trim();
                motherboard = (maker, product) switch
                {
                    (null, null) => null,
                    (null, { } p) => p,
                    ({ } m, null) => m,
                    ({ } m, { } p) => string.Equals(m, p, StringComparison.OrdinalIgnoreCase)
                        ? m
                        : $"{m} {p}",
                };
            }

            return new FirmwareInfo
            {
                Mode = NativeFirmware.GetMode(),
                BiosVersion = bios?.Version,
                BiosManufacturer = bios?.Manufacturer,
                BiosReleaseDate = bios?.Date,
                SmbiosVersion = bios?.Smbios,
                Motherboard = motherboard,
            };
        }
        catch
        {
            return FirmwareInfo.Unknown;
        }
    }
}

internal static class SecurityProvider
{
    // VBS/HVCI source priority: Win32_DeviceGuard WMI (authoritative runtime
    // state, same data msinfo32 shows) first, registry policy second. A missing
    // registry value means "never configured", not "off", so registry alone
    // cannot distinguish Disabled from Unknown.
    private const string DeviceGuardNamespace = @"root\Microsoft\Windows\DeviceGuard";

    // SecurityServices id 2 == Memory integrity (HVCI). Full table: 0 none,
    // 1 Credential Guard, 2 HVCI, 3 Secure Launch, 4 SMM measurement.
    private const int HypervisorEnforcedCodeIntegrityId = 2;

    public static SecurityInfo Get()
    {
        try
        {
            var firmwareMode = NativeFirmware.GetMode();
            var guard = ReadDeviceGuard();
            return new SecurityInfo
            {
                SecureBoot = ReadSecureBoot(firmwareMode),
                VirtualizationBasedSecurity = guard?.Status is { } status
                    ? MapVbsStatus(status, firmwareMode)
                    : ReadDwordState(
                        @"SYSTEM\CurrentControlSet\Control\DeviceGuard",
                        "EnableVirtualizationBasedSecurity"
                    ),
                MemoryIntegrity = guard is not null
                    ? MapMemoryIntegrity(guard.Running)
                    : ReadDwordState(
                        @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity",
                        "Enabled"
                    ),
            };
        }
        catch
        {
            return SecurityInfo.Unknown;
        }
    }

    private sealed record DeviceGuardState(int? Status, List<int> Running);

    private static DeviceGuardState? ReadDeviceGuard()
    {
        // May throw without elevation or on old OS: caller maps that to Unknown.
        return WmiHelper.QueryFirst(
            "SELECT VirtualizationBasedSecurityStatus, SecurityServicesRunning FROM Win32_DeviceGuard",
            static mo => new DeviceGuardState(
                WmiHelper.GetInt(mo, "VirtualizationBasedSecurityStatus"),
                ToServiceIds(mo["SecurityServicesRunning"])
            ),
            DeviceGuardNamespace
        );
    }

    /// <summary>
    /// VirtualizationBasedSecurityStatus: 0 not enabled, 1 enabled but not
    /// running, 2 enabled and running. Both 1 and 2 mean "enabled".
    /// </summary>
    internal static VbsState MapVbsStatus(int status, FirmwareMode firmwareMode)
    {
        if (firmwareMode == FirmwareMode.Legacy)
            return VbsState.Unavailable;
        return status switch
        {
            0 => VbsState.Disabled,
            1 or 2 => VbsState.Enabled,
            _ => VbsState.Unknown,
        };
    }

    /// <summary>Effective state: HVCI protects only when actually running.</summary>
    internal static VbsState MapMemoryIntegrity(List<int> runningServices)
    {
        return runningServices.Contains(HypervisorEnforcedCodeIntegrityId)
            ? VbsState.Enabled
            : VbsState.Disabled;
    }

    internal static List<int> ToServiceIds(object? value)
    {
        var ids = new List<int>();
        switch (value)
        {
            case Array arr:
                foreach (var item in arr)
                {
                    try
                    {
                        ids.Add(Convert.ToInt32(item));
                    }
                    catch
                    {
                        // Skip one malformed entry, keep the rest.
                    }
                }
                break;
            case null:
                break;
            default:
                try
                {
                    ids.Add(Convert.ToInt32(value));
                }
                catch
                {
                    // Unparseable single value: treat as no services.
                }
                break;
        }
        return ids;
    }

    private static SecureBootState ReadSecureBoot(FirmwareMode mode)
    {
        // Secure Boot cannot exist on Legacy boot: report Unavailable, not Disabled.
        if (mode == FirmwareMode.Legacy)
            return SecureBootState.Unavailable;
        return ReadDword(
            @"SYSTEM\CurrentControlSet\Control\SecureBoot\State",
            "UEFISecureBootEnabled"
        ) switch
        {
            1 => SecureBootState.Enabled,
            0 => SecureBootState.Disabled,
            _ => SecureBootState.Unknown,
        };
    }

    private static VbsState ReadDwordState(string subKey, string valueName)
    {
        return ReadDword(subKey, valueName) switch
        {
            1 => VbsState.Enabled,
            0 => VbsState.Disabled,
            _ => VbsState.Unknown,
        };
    }

    private static int? ReadDword(string subKey, string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(subKey);
            return key?.GetValue(valueName) switch
            {
                int i => i,
                uint u => (int)u,
                long l => (int)l,
                _ => null,
            };
        }
        catch
        {
            // Missing key or access denied: Unknown, never Disabled.
            return null;
        }
    }
}

internal static class PowerProvider
{
    public static PowerInfo Get()
    {
        try
        {
            var (id, name) = PowerReader.ReadActive();
            if (id is null)
                return PowerInfo.Unknown;
            return new PowerInfo { SchemeId = id.Value, SchemeName = name };
        }
        catch
        {
            return PowerInfo.Unknown;
        }
    }
}

internal static class RuntimeProvider
{
    public static RuntimeInfo Get()
    {
        try
        {
            return new RuntimeInfo
            {
                Uptime = TimeSpan.FromMilliseconds((double)Environment.TickCount64),
            };
        }
        catch
        {
            return RuntimeInfo.Unknown;
        }
    }
}

// ============================================================================
// MAIN SERVICE (public API)
// ============================================================================

public sealed class SystemInfoService : IDisposable
{
    private static readonly TimeSpan ScanTimeout = TimeSpan.FromSeconds(20);

    private readonly ILogger<SystemInfoService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // Static sections: resolved once, reused by every later refresh.
    private CpuInfo? _cachedCpu;
    private WindowsInfo? _cachedWindows;
    private FirmwareInfo? _cachedFirmware;
    private IReadOnlyList<GpuInfo>? _cachedGpus;

    public SystemInfoService(ILogger<SystemInfoService> logger, PowerPlanService? powerPlans = null)
    {
        _logger = logger;
        WmiHelper.ConfigureFailureLogger(
            (message, ex) => _logger.LogDebug(ex, "WMI: {Message}", message)
        );
        if (powerPlans is not null)
            PowerReader.Configure(powerPlans);
    }

    public SystemInfo Snapshot { get; private set; } = SystemInfo.Unknown;

    /// <summary>
    /// Raised after a refresh completes. Fires on the thread that finished the
    /// refresh (usually a pool thread): subscribers must marshal to UI.
    /// </summary>
    public event EventHandler<SystemInfo>? SnapshotRefreshed;

    /// <summary>
    /// Returns the current snapshot, refreshing first when nothing loaded yet.
    /// </summary>
    public async Task<SystemInfo> EnsureSnapshotAsync(CancellationToken ct = default)
    {
        if (!Snapshot.IsUnknown)
            return Snapshot;
        return await RefreshAsync(ct).ConfigureAwait(false);
    }

    public async Task<SystemInfo> RefreshAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Snapshot = await ScanAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }

        // After release so handlers may safely call back into RefreshAsync.
        // Multicast-safe: one failing handler never skips the rest.
        var snapshot = Snapshot;
        foreach (EventHandler<SystemInfo> handler in SnapshotRefreshed?.GetInvocationList() ?? [])
        {
            try
            {
                handler(this, snapshot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Snapshot refresh notification handler failed");
            }
        }

        return snapshot;
    }

    /// <summary>
    /// Live memory for high-frequency UI ticks. Cheap after the first full scan
    /// (module topology is cached); never touches WMI on the hot path itself.
    /// </summary>
    public MemoryInfo GetLiveMemory()
    {
        return MemoryProvider.Get();
    }

    /// <summary>
    /// Live storage sizes for high-frequency UI ticks. Uses DriveInfo plus the
    /// cached topology only: issues zero new WMI queries.
    /// </summary>
    public StorageInfo GetLiveStorage()
    {
        return DiskProvider.GetFast();
    }

    private async Task<SystemInfo> ScanAsync(CancellationToken ct)
    {
        var firstRun = _cachedCpu is null;

        // Cheap synchronous sections: no Task.Run needed, none of them block.
        var power = PowerProvider.Get();
        var runtime = RuntimeProvider.Get();
        var security = SecurityProvider.Get();

        Task<CpuInfo>? cpuTask = null;
        Task<WindowsInfo>? windowsTask = null;
        Task<FirmwareInfo>? firmwareTask = null;
        Task<IReadOnlyList<GpuInfo>>? gpusTask = null;

        if (firstRun)
        {
            cpuTask = Task.Run(CpuProvider.Get, ct);
            windowsTask = Task.Run(WindowsProvider.Get, ct);
            firmwareTask = Task.Run(FirmwareProvider.Get, ct);
            gpusTask = Task.Run(GpuProvider.GetAll, ct);
        }

        var memoryTask = Task.Run(MemoryProvider.Get, ct);
        var storageTask = Task.Run(DiskProvider.GetFull, ct);

        var pending = new List<Task>(capacity: 6);
        if (cpuTask is not null)
            pending.AddRange([cpuTask, windowsTask!, firmwareTask!, gpusTask!]);
        pending.AddRange([memoryTask, storageTask]);

        try
        {
            // Bounds a hung WMI call: on timeout the scan completes with the
            // sections that did finish instead of hanging the Dashboard.
            await Task.WhenAll(pending).WaitAsync(ScanTimeout, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("System info scan timed out; using partial results");
        }

        // User cancellation still aborts: never silently return partial data for it.
        ct.ThrowIfCancellationRequested();

        if (firstRun)
        {
            _cachedCpu = Take(cpuTask!, CpuInfo.Unknown);
            _cachedWindows = Take(windowsTask!, WindowsInfo.Unknown);
            _cachedFirmware = Take(firmwareTask!, FirmwareInfo.Unknown);
            _cachedGpus = Take(gpusTask!, (IReadOnlyList<GpuInfo>)[]);
        }

        var gpus = _cachedGpus ?? [];
        return new SystemInfo
        {
            Windows = _cachedWindows ?? WindowsInfo.Unknown,
            Cpu = _cachedCpu ?? CpuInfo.Unknown,
            Memory = Take(memoryTask, MemoryInfo.Unknown),
            Gpus = gpus,
            PrimaryGpu = GpuProvider.GetPrimary(gpus),
            Firmware = _cachedFirmware ?? FirmwareInfo.Unknown,
            Security = security,
            Power = power,
            Storage = Take(storageTask, StorageInfo.Unknown),
            Runtime = runtime,
        };

        static T Take<T>(Task<T> task, T fallback)
        {
            // Only already-finished results: never block on a timed-out WMI call.
            // Providers never throw, so non-success means cancelled/abandoned.
            return task.IsCompletedSuccessfully ? task.Result : fallback;
        }
    }

    public void LogSummary()
    {
        try
        {
            var s = Snapshot;
            _logger.LogInformation(
                "OS: Windows {Build} {Edition} [{Arch}] ({Device})",
                s.Windows.BuildNumber?.ToString() ?? "?",
                s.Windows.Edition,
                s.Windows.Architecture,
                s.Windows.DeviceKind
            );
            _logger.LogInformation(
                "CPU: {Cpu} [{Vendor}] ({Cores}C/{Threads}T)",
                s.Cpu.Name ?? "?",
                s.Cpu.Vendor,
                s.Cpu.CoreCount,
                s.Cpu.ThreadCount
            );
            _logger.LogInformation(
                "RAM: {Total:F1} GB ({Modules} modules, {Speed} MT/s {Type})",
                s.Memory.TotalGB,
                s.Memory.Modules.Count,
                s.Memory.SpeedMTps?.ToString() ?? "?",
                s.Memory.Type
            );
            foreach (var gpu in s.Gpus)
                _logger.LogInformation(
                    "GPU: {Gpu} [{Vendor}] ({Vram} MB)",
                    gpu.Name ?? "?",
                    gpu.Vendor,
                    gpu.VramMB?.ToString() ?? "?"
                );
            _logger.LogInformation(
                "Firmware: {Mode}, SecureBoot: {SecureBoot}, VBS: {Vbs}, Power: {Power}",
                s.Firmware.Mode,
                s.Security.SecureBoot,
                s.Security.VirtualizationBasedSecurity,
                s.Power.SchemeName ?? s.Power.SchemeId?.ToString() ?? "?"
            );
            foreach (var v in s.Storage.Volumes)
                _logger.LogInformation(
                    "Disk {Letter}{System} [{Media}] {Total} GB (free {Free} GB){Model}",
                    v.DriveLetter,
                    v.IsSystemDrive ? " [System]" : "",
                    v.MediaType,
                    // Integer division on purpose: a whole-gigabyte figure reads like Explorer's,
                    // while the double it came from would log 109.17089462280273.
                    v.TotalBytes / (1024L * 1024 * 1024),
                    v.FreeBytes / (1024L * 1024 * 1024),
                    v.Model is not null ? $" - {v.Model}" : ""
                );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log system summary");
        }
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
