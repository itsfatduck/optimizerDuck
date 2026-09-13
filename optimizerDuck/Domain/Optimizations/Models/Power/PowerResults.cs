using optimizerDuck.Domain.Execution;

namespace optimizerDuck.Domain.Optimizations.Models.Power;

/// <summary>Activation outcome plus the previously active scheme id (null on failure).</summary>
public sealed record ActivationResult(OpResult Result, Guid? PreviousSchemeId);

/// <summary>Setting-write outcome plus the previous AC/DC values (null on failure).</summary>
public sealed record SettingWriteResult(
    OpResult Result,
    uint? PreviousAcValue,
    uint? PreviousDcValue
);

/// <summary>Duplicate/import outcome plus the resulting scheme id (null on failure).</summary>
public sealed record SchemeRefResult(OpResult Result, Guid? SchemeId);

/// <summary>Atomic install outcome plus installed and previous ids (null on failure).</summary>
public sealed record InstallResult(OpResult Result, Guid? InstalledId, Guid? PreviousId);
