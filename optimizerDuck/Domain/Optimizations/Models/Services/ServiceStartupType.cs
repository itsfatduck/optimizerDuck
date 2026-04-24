namespace optimizerDuck.Domain.Optimizations.Models.Services;

/// <summary>
///     Specifies the startup type for a Windows service.
/// </summary>
public enum ServiceStartupType
{
    /// <summary>The service starts automatically at system startup.</summary>
    Automatic,

    /// <summary>The service starts only when explicitly started by a user or application.</summary>
    Manual,

    /// <summary>The service is disabled and cannot be started.</summary>
    Disabled,

    /// <summary>The service starts automatically after a short delay at system startup.</summary>
    AutomaticDelayedStart,
}
