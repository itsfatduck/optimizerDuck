namespace optimizerDuck.Services.System;

/// <summary>
/// A Windows power scheme (power plan): GUID identity plus the live metadata
/// Windows reports for it. <see cref="Description"/> is null when Windows has
/// none; <see cref="Name"/> falls back to the GUID string in that case.
/// </summary>
public sealed record PowerScheme
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public bool IsActive { get; init; }
}

/// <summary>A subgroup of power settings inside one scheme.</summary>
public sealed record PowerSettingGroup
{
    public required Guid SchemeId { get; init; }

    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }
}

/// <summary>Which mains/battery value a call targets. No magic 0/1 at call sites.</summary>
public enum PowerSource
{
    Ac,
    Dc,
}

/// <summary>
/// How much Windows tells us about a setting's shape. <c>Unknown</c> means
/// Windows exposed no usable metadata: values stay raw numbers, nothing is
/// invented. There is deliberately no per-setting enum type: most settings
/// are plain DWORD indexes whose friendly names live elsewhere or nowhere.
/// </summary>
public enum PowerValueKind
{
    Unknown,
    Range,
    Indexed,
}

/// <summary>
/// One named choice of an indexed setting, read live from Windows via
/// <c>PowerReadPossibleFriendlyName</c> / <c>PowerReadPossibleDescription</c>.
/// Index is positional (the PossibleSettingIndex Windows was asked for).
/// </summary>
public sealed record PowerPossibleValue
{
    public required uint Index { get; init; }

    public required uint Value { get; init; }

    public string? Name { get; init; }

    public string? Description { get; init; }
}

/// <summary>
/// A single power setting: identity plus raw AC/DC values exactly as Windows
/// stores them (DWORD indexes), with optional range/enumeration metadata.
/// Raw vs semantic vs display: <see cref="AcValue"/>/<see cref="DcValue"/>
/// are the raw numbers; <see cref="Kind"/>, <see cref="MinValue"/>,
/// <see cref="MaxValue"/>, <see cref="Unit"/>, and
/// <see cref="PossibleValues"/> describe semantics when Windows provides
/// them; friendly display text is built by callers, never stored here.
/// </summary>
public sealed record PowerSetting
{
    public required Guid SchemeId { get; init; }

    public required Guid GroupId { get; init; }

    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public PowerValueKind Kind { get; init; } = PowerValueKind.Unknown;

    public required uint AcValue { get; init; }

    public required uint DcValue { get; init; }

    public uint? MinValue { get; init; }

    public uint? MaxValue { get; init; }

    public uint? Increment { get; init; }

    public string? Unit { get; init; }

    public IReadOnlyList<PowerPossibleValue>? PossibleValues { get; init; }
}

/// <summary>
/// A native power-operation failure with the truth callers need: what ran,
/// on which identifiers, the real Win32 code, and the throw text when the
/// interop itself threw (never a fabricated sentinel code).
/// </summary>
public sealed record PowerPlanError
{
    public required string Operation { get; init; }

    public Guid? SchemeId { get; init; }

    public Guid? SubgroupId { get; init; }

    public Guid? SettingId { get; init; }

    public PowerSource? Source { get; init; }

    public string? Path { get; init; }

    /// <summary>Win32 error code, or null when the failure was a throw.</summary>
    public uint? ErrorCode { get; init; }

    /// <summary>Throw text when the interop threw; null for plain error codes.</summary>
    public string? ExceptionText { get; init; }

    public string Describe()
    {
        var target = SettingId ?? SubgroupId ?? SchemeId;
        var where =
            target.HasValue ? $" {target}"
            : Path is not null ? $" {Path}"
            : "";
        var code = ErrorCode.HasValue ? $" Win32 {ErrorCode}" : "";
        var thrown = ExceptionText is not null ? $": {ExceptionText}" : "";
        return $"{Operation} failed{where}.{code}{thrown}".TrimEnd('.', ' ');
    }
}
