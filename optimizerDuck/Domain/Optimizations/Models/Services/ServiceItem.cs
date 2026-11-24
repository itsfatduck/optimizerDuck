using System.Diagnostics.CodeAnalysis;

namespace optimizerDuck.Domain.Optimizations.Models.Services;

/// <summary>
///     Controls how <c>ServiceProcessService</c> compares the current startup type against the target.
/// </summary>
public enum ServiceChangeMode
{
    /// <summary>Always sets the service to the requested startup type.</summary>
    Exact,

    /// <summary>
    ///     Only lowers the service startup aggressiveness (Automatic -&gt; AutomaticDelayedStart
    ///     -&gt; Manual -&gt; Disabled); skips services already at a stricter startup type.
    /// </summary>
    DowngradeOnly,
}

/// <summary>
///     Represents a Windows service to be configured with a specific startup type.
/// </summary>
public readonly record struct ServiceItem
{
    [SetsRequiredMembers]
    public ServiceItem(
        string name,
        ServiceStartupType startupType,
        ServiceChangeMode mode = ServiceChangeMode.Exact
    )
    {
        Name = name;
        StartupType = startupType;
        Mode = mode;
    }

    /// <summary>
    ///     The Windows service name (e.g., <c>"DiagTrack"</c>).
    /// </summary>
    public required string Name { get; init; }

    public required ServiceStartupType StartupType { get; init; }

    /// <summary>Gets how the target startup type is applied relative to the current state.</summary>
    public ServiceChangeMode Mode { get; init; } = ServiceChangeMode.Exact;

    /// <summary>Creates a safe <see cref="ServiceChangeMode.DowngradeOnly"/> item setting a service to <see cref="ServiceStartupType.Disabled"/>.</summary>
    public static ServiceItem Disable(string name) =>
        new(name, ServiceStartupType.Disabled, ServiceChangeMode.DowngradeOnly);

    /// <summary>Creates a safe <see cref="ServiceChangeMode.DowngradeOnly"/> item setting a service to <see cref="ServiceStartupType.Manual"/>.</summary>
    public static ServiceItem Manual(string name) =>
        new(name, ServiceStartupType.Manual, ServiceChangeMode.DowngradeOnly);
}
