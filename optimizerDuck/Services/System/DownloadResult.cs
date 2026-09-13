namespace optimizerDuck.Services.System;

/// <summary>Download outcome: <c>Ok</c> plus the full local path on success.</summary>
public sealed record DownloadResult(bool Ok, string? FilePath);
