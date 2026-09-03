namespace optimizerDuck.Domain.Optimizations.Models.Services;

/// <summary>Outcome of a service startup type change request.</summary>
public enum ServiceChangeResult
{
    /// <summary>The startup type was changed successfully.</summary>
    Success,

    /// <summary>The service does not exist on this system.</summary>
    NotFound,

    /// <summary>The service already has the requested startup type; no change was needed.</summary>
    AlreadyConfigured,

    /// <summary>
    ///     Windows denied the change (ERROR_ACCESS_DENIED). Some protected services
    ///     (AppXSvc, ClipSVC, wscsvc...) have a DACL that does not grant
    ///     SERVICE_CHANGE_CONFIG to Administrators. Retrying cannot succeed.
    /// </summary>
    AccessDenied,

    /// <summary>The change failed for a recoverable reason (transient error, timeout).</summary>
    Failed,
}
