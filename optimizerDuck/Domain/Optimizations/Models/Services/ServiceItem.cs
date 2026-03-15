using System.Diagnostics.CodeAnalysis;

namespace optimizerDuck.Domain.Optimizations.Models.Services;

/// <summary>
///     Represents a Windows service to be configured with a specific startup type.
/// </summary>
public readonly record struct ServiceItem
{
    /// <summary>
    ///     Initializes a new <see cref="ServiceItem" /> with the given service name and startup type.
    /// </summary>
    /// <param name="name">The Windows service name.</param>
    /// <param name="startupType">The desired startup type.</param>
    [SetsRequiredMembers]
    public ServiceItem(string name, ServiceStartupType startupType)
    {
        Name = name;
        StartupType = startupType;
    }

    /// <summary>
    ///     The Windows service name (e.g., <c>"DiagTrack"</c>).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     The desired startup type for the service.
    /// </summary>
    public required ServiceStartupType StartupType { get; init; }
}