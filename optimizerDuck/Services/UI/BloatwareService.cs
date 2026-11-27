using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Configuration;
using optimizerDuck.Domain.Optimizations.Models.Bloatware;
using optimizerDuck.Domain.Serialization;
using optimizerDuck.Services.System.Primitives;
using AppXPackage = optimizerDuck.Domain.Optimizations.Models.Bloatware.AppXPackage;

namespace optimizerDuck.Services.UI;

public partial class BloatwareService(
    ILogger<BloatwareService> logger,
    IOptionsMonitor<AppSettings> appOptionsMonitor,
    ShellService shellService
)
{
    private static readonly ConcurrentDictionary<string, BitmapSource?> LogoCache = new(
        StringComparer.OrdinalIgnoreCase
    );
    private static readonly Regex ScaleRegex = CreateScaleRegex();
    private static readonly Regex TargetSizeRegex = CreateTargetSizeRegex();
    private static readonly string[] SupportedImageExtensions = [".png", ".jpg", ".jpeg", ".ico"];

    [GeneratedRegex(@"(?:^|[._])scale-(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex CreateScaleRegex();

    [GeneratedRegex(@"(?:^|[._])targetsize-(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex CreateTargetSizeRegex();

    /// <summary>
    ///     Gets all removable AppX packages on the system.
    /// </summary>
    /// <returns>A list of <see cref="AppXPackage" />.</returns>
    public virtual async Task<List<AppXPackage>> GetAppXPackagesAsync()
    {
        try
        {
            var safePattern = string.Join("|", Shared.SafeApps.Select(Regex.Escape));
            var cautionPattern = string.Join("|", Shared.CautionApps.Select(Regex.Escape));

            var result = await shellService
                .QueryPowerShellAsync(
                    $$"""
                    $safeRegex = '{{safePattern}}'
                    $cautionRegex = '{{cautionPattern}}'

                    Get-AppxPackage |
                    Where-Object {
                        $_.NonRemovable -eq $false -and
                        $_.IsFramework -eq $false
                    } |
                    ForEach-Object {
                        $risk = "Unknown"

                        if ($_.Name -match $safeRegex) { $risk = "Safe" }
                        elseif ($_.Name -match $cautionRegex) { $risk = "Caution" }

                        [PSCustomObject]@{
                            Name            = $_.Name
                            PackageFullName = $_.PackageFullName
                            Publisher       = $_.Publisher
                            Version         = $_.Version.ToString()
                            InstallLocation = if ($_.InstallLocation) { $_.InstallLocation } else { "" }
                            Risk            = $risk
                        }
                    } | ConvertTo-Json -Depth 2
                    """
                )
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(result.Stdout))
            {
                if (!string.IsNullOrWhiteSpace(result.Stderr))
                    logger.LogWarning("Get-AppxPackage stderr: {Error}", result.Stderr);

                return [];
            }

            var apps = ParsePackages(result.Stdout);

            if (apps.Count == 0)
            {
                logger.LogInformation("No removable AppX packages found");
                return [];
            }

            apps.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            await Parallel
                .ForEachAsync(
                    apps,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 2, 8),
                    },
                    (app, _) =>
                    {
                        if (string.IsNullOrWhiteSpace(app.InstallLocation))
                            return ValueTask.CompletedTask;

                        try
                        {
                            var manifestLogo = ResolveLogo(app.InstallLocation);
                            if (manifestLogo != null)
                                app.LogoImage = manifestLogo;
                        }
                        catch { }

                        return ValueTask.CompletedTask;
                    }
                )
                .ConfigureAwait(false);

#if DEBUG
            // Mock AppX package with unusual characters for testing warning dialog in UI
            apps.Add(
                new AppXPackage
                {
                    Name = "Test.MockUnusualPackage$(whoami)",
                    PackageFullName =
                        "Test.MockUnusualPackage$(whoami)_1.0.0.0_neutral__invalid!format",
                    Publisher = "CN=TestMockPublisher",
                    Version = "1.0.0.0",
                    InstallLocation = string.Empty,
                    Risk = AppRisk.Caution,
                }
            );
#endif
            logger.LogInformation("Found {AppCount} AppX packages", apps.Count);

            return apps;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get AppX packages");
            return [];
        }
    }

    /// <summary>
    ///     Outcome of one package removal, derived from what the removal script reported.
    /// </summary>
    /// <param name="Failures">
    ///     One entry per refusal, empty when Windows removed the package or reported that nothing
    ///     was installed.
    /// </param>
    public sealed record AppXRemovalResult(IReadOnlyList<string> Failures)
    {
        /// <summary>Whether Windows reported nothing as refused.</summary>
        public bool Succeeded => Failures.Count == 0;
    }

    /// <summary>
    ///     Reads the per package lines the removal script prints. Pure, so the mapping from the
    ///     script's output to a result is testable without running PowerShell. The markers are
    ///     printed by the script itself rather than by Windows, so a localized PowerShell cannot
    ///     change them; a script edit has to move with this parser.
    /// </summary>
    internal static AppXRemovalResult ParseRemovalOutput(string stdout)
    {
        var failures = new List<string>();
        foreach (var line in stdout.Split('\n'))
        {
            var trimmed = line.Trim();
            if (
                trimmed.StartsWith("Failed:", StringComparison.Ordinal)
                || trimmed.StartsWith(
                    "Failed removing provisioned package:",
                    StringComparison.Ordinal
                )
            )
                failures.Add(trimmed);
        }

        return new AppXRemovalResult(failures);
    }

    /// <summary>
    ///     Parses the <c>ConvertTo-Json</c> output of the AppX query. PowerShell emits a bare
    ///     object for a single package and an array for several, so both shapes are accepted.
    /// </summary>
    internal static List<AppXPackage> ParsePackages(string stdout)
    {
        var json = stdout.AsSpan().TrimStart();
        if (json.IsEmpty)
            return [];

        if (json.StartsWith('['))
            return JsonSerializer.Deserialize(json, AppJsonContext.Default.ListAppXPackage) ?? [];

        var single = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppXPackage);
        return single is null ? [] : [single];
    }

    // Validates an AppX package name/identifier to prevent PowerShell injection.
    // AppX names follow: Publisher.PackageName (alphanumeric, dots, dashes)
    // PackageFullName follows: Name_Version_Architecture__ResourceId
    private static readonly Regex AppXNameValidationRegex = CreateAppXNameValidationRegex();

    private static readonly Regex AppXPackageFullNameValidationRegex =
        CreateAppXPackageFullNameValidationRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9._\-]+$")]
    private static partial Regex CreateAppXNameValidationRegex();

    [GeneratedRegex(
        @"^[a-zA-Z0-9._\-]+_[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+_[a-zA-Z0-9_]+__[a-zA-Z0-9_]+$"
    )]
    private static partial Regex CreateAppXPackageFullNameValidationRegex();

    /// <summary>
    ///     Validates whether an AppX package has standard, safe identifiers.
    /// </summary>
    public static bool IsValidPackage(AppXPackage package)
    {
        if (string.IsNullOrWhiteSpace(package.PackageFullName))
            return false;

        var nameValid =
            string.IsNullOrEmpty(package.Name) || AppXNameValidationRegex.IsMatch(package.Name);
        var fullNameValid = AppXPackageFullNameValidationRegex.IsMatch(package.PackageFullName);
        return nameValid && fullNameValid;
    }

    /// <summary>
    ///     Escapes a string value for safe embedding in a PowerShell single-quoted string literal.
    ///     Single quotes in PowerShell do not evaluate variables or subexpressions.
    /// </summary>
    private static string EscapeForPowerShellLiteral(string value)
    {
        return value.Replace("'", "''");
    }

    /// <summary>
    ///     Removes an AppX package from the system.
    /// </summary>
    public async Task<AppXRemovalResult> RemoveAppXPackage(AppXPackage appXPackage)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(appXPackage.PackageFullName))
            {
                logger.LogWarning(
                    "Skip removing app because PackageFullName is empty: {Name}",
                    appXPackage.Name
                );
                return new AppXRemovalResult([]);
            }

            var safeName = EscapeForPowerShellLiteral(appXPackage.Name ?? string.Empty);
            var safePackageFullName = EscapeForPowerShellLiteral(appXPackage.PackageFullName);
            if (!AppXNameValidationRegex.IsMatch(appXPackage.Name ?? string.Empty))
                logger.LogWarning(
                    "AppX package name contains unusual characters: {Name}",
                    appXPackage.Name
                );

            if (!AppXPackageFullNameValidationRegex.IsMatch(appXPackage.PackageFullName))
                logger.LogWarning(
                    "AppX PackageFullName has unexpected format: {PackageFullName}",
                    appXPackage.PackageFullName
                );

            logger.LogInformation(
                "Removing AppX package {Name} ({Package})",
                appXPackage.Name,
                appXPackage.PackageFullName
            );

            var script = $$"""
                $pkgFull = '{{safePackageFullName}}'
                $name = '{{safeName}}'

                $installed = Get-AppxPackage -PackageTypeFilter Main,Bundle,Resource |
                             Where-Object { $_.PackageFullName -eq $pkgFull -or $_.Name -eq $name }

                if (-not $installed) {
                    Write-Output "No installed package found for $name"
                } else {
                    foreach ($p in $installed) {
                        try {
                            Remove-AppxPackage -Package $p.PackageFullName -ErrorAction Stop
                            Write-Output "Removed: $($p.PackageFullName)"
                        } catch {
                            Write-Output "Failed: $($p.PackageFullName). Error: $($_.Exception.Message)"
                        }
                    }
                }

                """;

            if (appOptionsMonitor.CurrentValue.Bloatware.RemoveProvisioned)
                script += """
                    Write-Output "Searching provisioned package..."

                    $prov = Get-AppxProvisionedPackage -Online |
                            Where-Object { $_.DisplayName -eq $name }

                    if (-not $prov) {
                        Write-Output "Provisioned package not found"
                    }
                    else {
                        foreach ($p in $prov) {
                            try {
                                Remove-AppxProvisionedPackage -Online -PackageName $p.PackageName -ErrorAction Stop | Out-Null
                                Write-Output "Removed provisioned package: $($p.PackageName)"
                            }
                            catch {
                                Write-Output "Failed removing provisioned package: $($p.PackageName). Error: $($_.Exception.Message)"
                            }
                        }
                    }

                    """;
            else
                script += """
                    Write-Output "Skipping provisioned package removal (disabled by user)"
                    """;

            var result = await shellService
                .QueryPowerShellAsync(script, logger)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(result.Stderr))
                logger.LogWarning("Remove AppX stderr: {Error}", result.Stderr);

            var outcome = ParseRemovalOutput(result.Stdout);

            // A shell that exited non zero without printing a per package line never reported a
            // removal, so the captured error text stands in for it.
            if (outcome.Succeeded && result.ExitCode != 0)
                outcome = new AppXRemovalResult([result.Stderr]);

            if (outcome.Succeeded)
            {
                logger.LogInformation("Remove AppX finished for {Name}", appXPackage.Name);
            }
            else
            {
                foreach (var failure in outcome.Failures)
                    logger.LogError("[APPX][FAIL] {Name}: {Failure}", appXPackage.Name, failure);
            }

            return outcome;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed removing AppX package {Name}", appXPackage.Name);
            return new AppXRemovalResult([ex.Message]);
        }
    }

    private static BitmapSource? ResolveLogo(string installLocation)
    {
        if (string.IsNullOrWhiteSpace(installLocation))
            return null;

        if (LogoCache.TryGetValue(installLocation, out var cachedLogo))
            return cachedLogo;

        var resolvedPath = ResolveLogoCore(installLocation);
        var resolved =
            resolvedPath != null ? StartupManagerService.LoadFrozenBitmapImage(resolvedPath) : null;
        LogoCache[installLocation] = resolved;
        return resolved;
    }

    private static string? ResolveLogoCore(string installLocation)
    {
        try
        {
            var candidateResources = GetManifestLogoCandidates(installLocation);
            foreach (var candidate in candidateResources)
            {
                var bestFromCandidate = ResolveBestQualifiedVariant(
                    installLocation,
                    candidate,
                    includeThemeSpecific: true
                );
                if (!string.IsNullOrWhiteSpace(bestFromCandidate))
                    return bestFromCandidate;
            }

            var assetsDir = Path.Combine(installLocation, "Assets");
            if (Directory.Exists(assetsDir))
            {
                // Per Microsoft guidance, AppList/Square44x44 assets are a better primary app
                // icon source than StoreLogo.
                var fallbackCandidates = new[]
                {
                    @"Assets\AppList.png",
                    @"Assets\Square44x44Logo.png",
                    @"Assets\SmallTile.png",
                    @"Assets\MedTile.png",
                    @"Assets\Square150x150Logo.png",
                    @"Assets\Logo.png",
                    @"Assets\StoreLogo.png",
                };

                foreach (var fallbackCandidate in fallbackCandidates)
                {
                    var bestFallback = ResolveBestQualifiedVariant(
                        installLocation,
                        fallbackCandidate,
                        includeThemeSpecific: true
                    );
                    if (!string.IsNullOrWhiteSpace(bestFallback))
                        return bestFallback;
                }

                var allImageFiles = Directory
                    .EnumerateFiles(assetsDir, "*.*", SearchOption.AllDirectories)
                    .Where(path =>
                        SupportedImageExtensions.Contains(
                            Path.GetExtension(path),
                            StringComparer.OrdinalIgnoreCase
                        )
                    )
                    .ToList();

                if (allImageFiles.Count != 0)
                {
                    var groups = allImageFiles
                        .GroupBy(
                            path =>
                            {
                                var name = Path.GetFileNameWithoutExtension(path);
                                return StripKnownQualifiers(name);
                            },
                            StringComparer.OrdinalIgnoreCase
                        )
                        .OrderByDescending(g =>
                            g.Key.Contains("applist", StringComparison.OrdinalIgnoreCase) ? 2
                            : g.Key.Contains("square44x44", StringComparison.OrdinalIgnoreCase) ? 2
                            : g.Key.Contains("square150x150", StringComparison.OrdinalIgnoreCase)
                                ? 1
                            : g.Key.Contains("storelogo", StringComparison.OrdinalIgnoreCase) ? -1
                            : 0
                        )
                        .ThenByDescending(g => g.Count());

                    // Limit to first 10 groups to avoid excessive I/O for packages with many files
                    var groupCount = 0;
                    foreach (var group in groups)
                    {
                        if (++groupCount > 10)
                            break;

                        var preferredSize = GuessLogicalBaseSize(group.Key);
                        var best = group
                            .Select(path => new LogoVariant(path, preferredSize, true))
                            .MaxBy(v => v.SortScore);
                        if (best != null)
                            return best.Path;
                    }
                }
            }
        }
        catch
        {
            // ignored: returning null if any file access/parsing fails
        }

        return null;
    }

    private static List<string> GetManifestLogoCandidates(string installLocation)
    {
        var manifest = Path.Combine(installLocation, "AppxManifest.xml");
        if (!File.Exists(manifest))
            return [];

        var candidates = new List<string>();
        var doc = XDocument.Load(manifest);
        var root = doc.Root;
        if (root == null)
            return candidates;

        var ns = root.Name.Namespace;
        var visualElements = root.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "VisualElements");
        var defaultTile = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "DefaultTile");
        var properties = root.Element(ns + "Properties");

        if (visualElements != null)
        {
            AddAttr(candidates, visualElements, "Square44x44Logo");
            AddAttr(candidates, visualElements, "Square150x150Logo");
            AddAttr(candidates, visualElements, "Wide310x150Logo");
            AddAttr(candidates, visualElements, "Square310x310Logo");
        }

        if (defaultTile != null)
        {
            AddAttr(candidates, defaultTile, "Square71x71Logo");
            AddAttr(candidates, defaultTile, "Square310x310Logo");
            AddAttr(candidates, defaultTile, "Wide310x150Logo");
            AddAttr(candidates, defaultTile, "Square44x44Logo");
            AddAttr(candidates, defaultTile, "Square150x150Logo");
        }

        var packageLogo = properties?.Element(ns + "Logo")?.Value;
        if (!string.IsNullOrWhiteSpace(packageLogo))
            candidates.Add(packageLogo);

        return candidates
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? ResolveBestQualifiedVariant(
        string installLocation,
        string resourcePath,
        bool includeThemeSpecific
    )
    {
        var normalizedResource = resourcePath.Replace('/', '\\').TrimStart('\\');
        if (string.IsNullOrWhiteSpace(normalizedResource))
            return null;

        var fullExactPath = Path.Combine(installLocation, normalizedResource);
        if (File.Exists(fullExactPath))
            return fullExactPath;

        var candidateDirectory = Path.GetDirectoryName(fullExactPath) ?? installLocation;
        if (!Directory.Exists(candidateDirectory))
            return null;

        var resourceFileName = Path.GetFileName(normalizedResource);
        var extension = Path.GetExtension(resourceFileName);
        if (
            !string.IsNullOrWhiteSpace(extension)
            && !SupportedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
        )
            return null;

        var baseResourceName = Path.GetFileNameWithoutExtension(resourceFileName);
        var baseNameWithoutQualifiers = StripKnownQualifiers(baseResourceName);
        var preferredLogicalSize = GuessLogicalBaseSize(baseNameWithoutQualifiers);

        var best = FindBestLogoVariant(
            candidateDirectory,
            baseNameWithoutQualifiers,
            preferredLogicalSize,
            includeThemeSpecific,
            SearchOption.TopDirectoryOnly
        );

        if (best != null)
            return best.Path;

        return FindBestLogoVariant(
            candidateDirectory,
            baseNameWithoutQualifiers,
            preferredLogicalSize,
            includeThemeSpecific,
            SearchOption.AllDirectories
        )?.Path;
    }

    private static string StripKnownQualifiers(string fileNameWithoutExtension)
    {
        return StripKnownQualifiersRegex()
            .Replace(fileNameWithoutExtension, string.Empty)
            .TrimEnd('.', '_');
    }

    [GeneratedRegex(@"(?:[._](?:scale|targetsize)-\d+|_[^._]+)+$", RegexOptions.IgnoreCase)]
    private static partial Regex StripKnownQualifiersRegex();

    private static int GuessLogicalBaseSize(string baseName)
    {
        if (
            baseName.Contains("applist", StringComparison.OrdinalIgnoreCase)
            || baseName.Contains("square44x44", StringComparison.OrdinalIgnoreCase)
            || baseName.Contains("smalltile", StringComparison.OrdinalIgnoreCase)
        )
            return 44;

        if (
            baseName.Contains("medtile", StringComparison.OrdinalIgnoreCase)
            || baseName.Contains("square150x150", StringComparison.OrdinalIgnoreCase)
        )
            return 150;

        if (
            baseName.Contains("widetile", StringComparison.OrdinalIgnoreCase)
            || baseName.Contains("wide310x150", StringComparison.OrdinalIgnoreCase)
        )
            return 150;

        if (
            baseName.Contains("large", StringComparison.OrdinalIgnoreCase)
            || baseName.Contains("square310x310", StringComparison.OrdinalIgnoreCase)
        )
            return 310;

        if (baseName.Contains("storelogo", StringComparison.OrdinalIgnoreCase))
            return 50;

        return 64;
    }

    private sealed class LogoVariant
    {
        public LogoVariant(string path, int logicalBaseSize, bool includeThemeSpecific)
        {
            Path = path;
            var fileNameWithoutExtension = global::System.IO.Path.GetFileNameWithoutExtension(path);

            var targetSize = TryGetQualifierNumber(TargetSizeRegex, fileNameWithoutExtension);
            var scale = TryGetQualifierNumber(ScaleRegex, fileNameWithoutExtension);

            var resolvedPixelSize =
                targetSize
                ?? (scale.HasValue ? logicalBaseSize * scale.Value / 100 : logicalBaseSize);

            var score = 0;

            score += Math.Min(resolvedPixelSize * 5, 1500);

            if (resolvedPixelSize >= 256)
                score += 600;
            else if (resolvedPixelSize >= 96)
                score += 400;
            else if (resolvedPixelSize >= 64)
                score += 200;
            else if (resolvedPixelSize >= 48)
                score += 100;

            if (targetSize.HasValue)
                score += Math.Min(targetSize.Value * 3, 800);

            if (scale.HasValue)
                score += Math.Min(scale.Value * 2, 800);

            if (includeThemeSpecific)
            {
                if (
                    fileNameWithoutExtension.Contains(
                        "_altform-unplated",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    score += 200;
                if (
                    fileNameWithoutExtension.Contains(
                        "_altform-lightunplated",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    score += 180;
            }

            if (fileNameWithoutExtension.Contains("storelogo", StringComparison.OrdinalIgnoreCase))
                score -= 500;

            SortScore = score;
        }

        public string Path { get; }
        public int SortScore { get; }

        private static int? TryGetQualifierNumber(Regex regex, string text)
        {
            var match = regex.Match(text);
            if (!match.Success)
                return null;

            return int.TryParse(match.Groups[1].Value, out var value) ? value : null;
        }
    }

    private static void AddAttr(List<string> list, XElement el, string name)
    {
        var val = el.Attribute(name)?.Value;
        if (!string.IsNullOrWhiteSpace(val))
            list.Add(val);
    }

    private static LogoVariant? FindBestLogoVariant(
        string directory,
        string baseNameWithoutQualifiers,
        int preferredLogicalSize,
        bool includeThemeSpecific,
        SearchOption searchOption
    )
    {
        return Directory
            .EnumerateFiles(directory, "*.*", searchOption)
            .Where(path =>
                SupportedImageExtensions.Contains(
                    Path.GetExtension(path),
                    StringComparer.OrdinalIgnoreCase
                )
            )
            .Where(path =>
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                var stripped = StripKnownQualifiers(fileName);
                return stripped.Equals(
                    baseNameWithoutQualifiers,
                    StringComparison.OrdinalIgnoreCase
                );
            })
            .Select(path => new LogoVariant(path, preferredLogicalSize, includeThemeSpecific))
            .MaxBy(v => v.SortScore);
    }
}
