namespace optimizerDuck.Services.System;

/// <summary>CPU vendor. Unknown = detection failed, never guessed.</summary>
public enum CpuVendor
{
    Unknown,
    Intel,
    Amd,
}

/// <summary>GPU vendor, keyed by PCI vendor ID on the DXGI path.</summary>
public enum GpuVendor
{
    Unknown,
    Nvidia,
    Amd,
    Intel,
}

/// <summary>OS/CPU architecture.</summary>
public enum Architecture
{
    Unknown,
    X86,
    X64,
    Arm64,
}

/// <summary>Device form factor inferred from SMBIOS chassis type.</summary>
public enum DeviceKind
{
    Unknown,
    Desktop,
    Laptop,
}

/// <summary>Firmware boot mode. From GetFirmwareType; never inferred.</summary>
public enum FirmwareMode
{
    Unknown,
    Uefi,
    Legacy,
}

/// <summary>Secure Boot state. Unavailable = Legacy boot (no Secure Boot possible).</summary>
public enum SecureBootState
{
    Unknown,
    Enabled,
    Disabled,
    Unavailable,
}

/// <summary>Generic on/off/unknown state for VBS and Memory Integrity.</summary>
public enum VbsState
{
    Unknown,
    Enabled,
    Disabled,
    Unavailable,
}

/// <summary>RAM technology from SMBIOS memory type. Unknown when WMI hides it.</summary>
public enum MemoryType
{
    Unknown,
    Ddr,
    Ddr2,
    Ddr3,
    Ddr4,
    Ddr5,
    Lpddr,
    Hbm,
    Sdram,
    Other,
}

/// <summary>
/// Vendor overclock-profile state. Always <see cref="Unknown"/>: Windows exposes
/// no reliable XMP/EXPO flag (speed alone never implies a profile). Exists so
/// callers can distinguish "not detected" from a future reliable detection.
/// </summary>
public enum MemoryProfileState
{
    Unknown,
    Enabled,
    Disabled,
}

/// <summary>Physical storage kind. Nvme is split out because it matters for perf expectations.</summary>
public enum StorageMediaType
{
    Unknown,
    Hdd,
    Ssd,
    Nvme,
}

/// <summary>Windows edition from EditionID. Server SKUs collapse to Server.</summary>
public enum WindowsEdition
{
    Unknown,
    Home,
    Pro,
    Education,
    Enterprise,
    Server,
}

/// <summary>Operating system facts. All display text is resolved in UI layer.</summary>
public sealed record WindowsInfo
{
    public static readonly WindowsInfo Unknown = new();

    /// <summary>Major build number (e.g. 26100). Null when unreadable.</summary>
    public int? BuildNumber { get; init; }

    /// <summary>Display version (e.g. "24H2"). Null when unreadable.</summary>
    public string? DisplayVersion { get; init; }

    public WindowsEdition Edition { get; init; } = WindowsEdition.Unknown;
    public Architecture Architecture { get; init; } = Architecture.Unknown;
    public DeviceKind DeviceKind { get; init; } = DeviceKind.Unknown;
    public DateTime? InstallDate { get; init; }
    public DateTime? LastBootTime { get; init; }

    public bool IsWindows11 => BuildNumber is >= 22000;
    public bool IsWindows10 => BuildNumber is >= 10240 and < 22000;
}

/// <summary>CPU facts. Frequencies are best-effort informational, never gating.</summary>
public sealed record CpuInfo
{
    public static readonly CpuInfo Unknown = new();

    public string? Name { get; init; }
    public CpuVendor Vendor { get; init; } = CpuVendor.Unknown;
    public Architecture Architecture { get; init; } = Architecture.Unknown;
    public int CoreCount { get; init; }
    public int ThreadCount { get; init; }
    public int? MaxFrequencyMhz { get; init; }
    public int? CurrentFrequencyMhz { get; init; }
    public int? L2CacheKB { get; init; }
    public int? L3CacheKB { get; init; }

    /// <summary>Null when firmware flag unreadable (Win32_Processor.VirtualizationFirmwareEnabled).</summary>
    public bool? VirtualizationFirmwareEnabled { get; init; }

    public bool IsIntel => Vendor == CpuVendor.Intel;
    public bool IsAmd => Vendor == CpuVendor.Amd;
}

/// <summary>Single GPU. Null VramMB/DriverVersion = not reported, not zero.</summary>
public sealed record GpuInfo
{
    public static readonly GpuInfo Unknown = new();

    public string? Name { get; init; }
    public GpuVendor Vendor { get; init; } = GpuVendor.Unknown;
    public int? VramMB { get; init; }
    public string? DriverVersion { get; init; }
    public DateTime? DriverDate { get; init; }
    public string? DeviceId { get; init; }
    public string? PnpDeviceId { get; init; }

    public bool IsNvidia => Vendor == GpuVendor.Nvidia;
    public bool IsAmd => Vendor == GpuVendor.Amd;
    public bool IsIntel => Vendor == GpuVendor.Intel;
}

/// <summary>One physical RAM stick. Null numeric = slot reports nothing usable.</summary>
public sealed record MemoryModuleInfo
{
    public double CapacityGB { get; init; }
    public long CapacityBytes { get; init; }
    public int? SpeedMTps { get; init; }
    public MemoryType Type { get; init; } = MemoryType.Unknown;
    public string? Manufacturer { get; init; }
    public string? PartNumber { get; init; }
    public string? Slot { get; init; }
}

/// <summary>Memory facts. Totals from GlobalMemoryStatusEx (Task Manager accuracy).</summary>
public sealed record MemoryInfo
{
    public static readonly MemoryInfo Unknown = new() { Modules = [] };

    public long TotalBytes { get; init; }
    public long AvailableBytes { get; init; }

    /// <summary>Max configured clock across modules, MT/s. Null when unreported.</summary>
    public int? SpeedMTps { get; init; }

    public MemoryType Type { get; init; } = MemoryType.Unknown;

    /// <summary>Always Unknown today; kept so callers never invent it from speed.</summary>
    public MemoryProfileState OverclockProfile { get; init; } = MemoryProfileState.Unknown;

    public required IReadOnlyList<MemoryModuleInfo> Modules { get; init; }

    public long UsedBytes => Math.Max(0, TotalBytes - AvailableBytes);
    public double TotalGB => TotalBytes / (1024.0 * 1024.0 * 1024.0);
    public long TotalKB => TotalBytes / 1024;
    public double UsedPercent => TotalBytes > 0 ? UsedBytes * 100.0 / TotalBytes : 0;
}

/// <summary>Firmware + board facts. Mode from GetFirmwareType, never guessed.</summary>
public sealed record FirmwareInfo
{
    public static readonly FirmwareInfo Unknown = new();

    public FirmwareMode Mode { get; init; } = FirmwareMode.Unknown;
    public string? BiosVersion { get; init; }
    public string? BiosManufacturer { get; init; }
    public DateTime? BiosReleaseDate { get; init; }
    public string? SmbiosVersion { get; init; }
    public string? Motherboard { get; init; }
}

/// <summary>Security states. Unknown = could not read; Unavailable = not applicable.</summary>
public sealed record SecurityInfo
{
    public static readonly SecurityInfo Unknown = new();

    public SecureBootState SecureBoot { get; init; } = SecureBootState.Unknown;
    public VbsState VirtualizationBasedSecurity { get; init; } = VbsState.Unknown;
    public VbsState MemoryIntegrity { get; init; } = VbsState.Unknown;
}

/// <summary>
/// Active power scheme. Names come live from Windows
/// (<c>PowerReadFriendlyName</c>), never from a hardcoded table:
/// OEM and custom schemes have arbitrary names.
/// Null <see cref="SchemeId"/> means the active scheme could not be read.
/// </summary>
public sealed record PowerInfo
{
    public static readonly PowerInfo Unknown = new();

    public Guid? SchemeId { get; init; }
    public string? SchemeName { get; init; }

    public bool IsOptimizerDuckScheme(Guid optimizerDuckSchemeId) =>
        SchemeId == optimizerDuckSchemeId && optimizerDuckSchemeId != Guid.Empty;
}

/// <summary>One logical volume; Model/MediaType join physical disk when resolvable.</summary>
public sealed record StorageVolume
{
    public required string DriveLetter { get; init; }
    public bool IsSystemDrive { get; init; }
    public string? Label { get; init; }
    public string? FileSystem { get; init; }
    public long TotalBytes { get; init; }
    public long FreeBytes { get; init; }
    public StorageMediaType MediaType { get; init; } = StorageMediaType.Unknown;
    public string? Model { get; init; }
    public bool IsRemovable { get; init; }

    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);
    public double UsedPercent => TotalBytes > 0 ? UsedBytes * 100.0 / TotalBytes : 0;
}

public sealed record StorageInfo
{
    public static readonly StorageInfo Unknown = new() { Volumes = [] };
    public required IReadOnlyList<StorageVolume> Volumes { get; init; }
}

/// <summary>Cheap always-available runtime facts.</summary>
public sealed record RuntimeInfo
{
    public static readonly RuntimeInfo Unknown = new();
    public TimeSpan Uptime { get; init; }
}

/// <summary>
/// Root snapshot. Sections fail independently: one Unknown section never
/// poisons the rest. <see cref="IsUnknown"/> marks a fully failed scan
/// (conditions fail open on it).
/// </summary>
public sealed record SystemInfo
{
    public static readonly SystemInfo Unknown = new()
    {
        IsUnknown = true,
        Windows = WindowsInfo.Unknown,
        Cpu = CpuInfo.Unknown,
        Memory = MemoryInfo.Unknown,
        Gpus = [],
        Firmware = FirmwareInfo.Unknown,
        Security = SecurityInfo.Unknown,
        Power = PowerInfo.Unknown,
        Storage = StorageInfo.Unknown,
        Runtime = RuntimeInfo.Unknown,
    };

    public bool IsUnknown { get; init; }
    public required WindowsInfo Windows { get; init; }
    public required CpuInfo Cpu { get; init; }
    public required MemoryInfo Memory { get; init; }
    public required IReadOnlyList<GpuInfo> Gpus { get; init; }
    public GpuInfo? PrimaryGpu { get; init; }
    public required FirmwareInfo Firmware { get; init; }
    public required SecurityInfo Security { get; init; }
    public required PowerInfo Power { get; init; }
    public required StorageInfo Storage { get; init; }
    public required RuntimeInfo Runtime { get; init; }
}
