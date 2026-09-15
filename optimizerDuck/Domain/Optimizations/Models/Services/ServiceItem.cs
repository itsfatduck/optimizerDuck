using System.Diagnostics.CodeAnalysis;

namespace optimizerDuck.Domain.Optimizations.Models.Services;

/// <summary>
///     Represents a Windows service to be configured with a specific startup type.
/// </summary>
public readonly record struct ServiceItem
{
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

    public required ServiceStartupType StartupType { get; init; }
}
