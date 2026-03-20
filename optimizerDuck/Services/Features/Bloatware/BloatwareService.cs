using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Configuration;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Services.OptimizationServices;
using AppXPackage = optimizerDuck.Domain.Optimizations.Models.Bloatware.AppXPackage;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace optimizerDuck.Services;

/// <summary>
///     Provides services for discovering and removing AppX bloatware packages.
/// </summary>
public class BloatwareService(ILogger<BloatwareService> logger, IOptionsMonitor<AppSettings> appOptionsMonitor)
{
    private static readonly ConcurrentDictionary<string, string?> LogoCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex ScaleRegex = new(@"(?:^|[._])scale-(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TargetSizeRegex = new(@"(?:^|[._])targetsize-(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string[] SupportedImageExtensions = [".png", ".jpg", ".jpeg"];

    /// <summary>
    ///     Gets all removable AppX packages on the system.
    /// </summary>
    /// <returns>A list of <see cref="AppXPackage" />.</returns>
    public async Task<List<AppXPackage>> GetAppXPackagesAsync()
    {
        try
        {
            var safePattern = string.Join("|", Shared.SafeApps.Select(Regex.Escape));
            var cautionPattern = string.Join("|", Shared.CautionApps.Select(Regex.Escape));

            var result = await ShellService.PowerShellAsync($$"""
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
                                                              """);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            var apps = JsonSerializer.Deserialize<List<AppXPackage>>(result.Stdout, options)!.OrderBy(a => a.Name)
                .ToList();

            foreach (var app in apps)
            {
                if (string.IsNullOrWhiteSpace(app.InstallLocation))
                    continue;

                try
                {
                    var manifestLogo = ResolveLogo(app.InstallLocation);
                    if (manifestLogo != null)
                        app.LogoImage = manifestLogo;
                }
                catch
                {
                    /* Ignore access exceptions */
                }
            }

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
    ///     Removes an AppX package from the system.
    /// </summary>
    /// <param name="appXPackage">The package to remove.</param>
    public async Task RemoveAppXPackage(AppXPackage appXPackage)
    {
        using var scope = ExecutionScope.BeginForLogging(logger);
        try
        {
            if (string.IsNullOrWhiteSpace(appXPackage.PackageFullName))
            {
                logger.LogWarning("Skip removing app because PackageFullName is empty: {Name}", appXPackage.Name);
                return;
            }

            logger.LogInformation("Removing AppX package {Name} ({Package})",
                appXPackage.Name, appXPackage.PackageFullName);

            var script = $$"""
                           $pkgFull = "{{appXPackage.PackageFullName}}"
                           $name = "{{appXPackage.Name}}"

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

            var result = await ShellService.PowerShellAsync(script);

            if (!string.IsNullOrWhiteSpace(result.Stderr))
                logger.LogWarning("Remove AppX stderr: {Error}", result.Stderr);

            logger.LogInformation("Remove AppX finished for {Name}", appXPackage.Name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed removing AppX package {Name}", appXPackage.Name);
        }
    }

    private static string? ResolveLogo(string installLocation)
    {
        if (string.IsNullOrWhiteSpace(installLocation))
            return null;

        if (LogoCache.TryGetValue(installLocation, out var cachedLogo))
            return cachedLogo;

        var resolved = ResolveLogoCore(installLocation);
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
                var bestFromCandidate = ResolveBestQualifiedVariant(installLocation, candidate,
                    includeThemeSpecific: true);
                if (!string.IsNullOrWhiteSpace(bestFromCandidate))
                    return bestFromCandidate;
            }

            var assetsDir = Path.Combine(installLocation, "Assets");
            if (!Directory.Exists(assetsDir))
                return null;

            // Per Microsoft guidance, AppList/Square44x44 assets are a better primary app icon source than StoreLogo.
            var fallbackCandidates = new[]
            {
                @"Assets\AppList.png",
                @"Assets\Square44x44Logo.png",
                @"Assets\SmallTile.png",
                @"Assets\MedTile.png",
                @"Assets\Square150x150Logo.png",
                @"Assets\Logo.png",
                @"Assets\StoreLogo.png"
            };

            foreach (var fallbackCandidate in fallbackCandidates)
            {
                var bestFallback = ResolveBestQualifiedVariant(installLocation, fallbackCandidate,
                    includeThemeSpecific: true);
                if (!string.IsNullOrWhiteSpace(bestFallback))
                    return bestFallback;
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
        var visualElements = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "VisualElements");
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

    private static string? ResolveBestQualifiedVariant(string installLocation, string resourcePath,
        bool includeThemeSpecific)
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
        if (!string.IsNullOrWhiteSpace(extension) &&
            !SupportedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return null;

        var baseResourceName = Path.GetFileNameWithoutExtension(resourceFileName);
        var baseNameWithoutQualifiers = StripKnownQualifiers(baseResourceName);
        var preferredLogicalSize = GuessLogicalBaseSize(baseNameWithoutQualifiers);

        var files = Directory.EnumerateFiles(candidateDirectory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => SupportedImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Where(path =>
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                var stripped = StripKnownQualifiers(fileName);
                return stripped.Equals(baseNameWithoutQualifiers, StringComparison.OrdinalIgnoreCase);
            })
            .Select(path => new LogoVariant(path, preferredLogicalSize, includeThemeSpecific))
            .OrderByDescending(v => v.SortScore)
            .ToList();

        return files.FirstOrDefault()?.Path;
    }

    private static string StripKnownQualifiers(string fileNameWithoutExtension)
    {
        return Regex.Replace(
            fileNameWithoutExtension,
            @"(?:[._](?:scale|targetsize)-\d+|_[^._]+)+$",
            string.Empty,
            RegexOptions.IgnoreCase).TrimEnd('.', '_');
    }

    private static int GuessLogicalBaseSize(string baseName)
    {
        if (baseName.Contains("applist", StringComparison.OrdinalIgnoreCase) ||
            baseName.Contains("square44x44", StringComparison.OrdinalIgnoreCase) ||
            baseName.Contains("smalltile", StringComparison.OrdinalIgnoreCase))
            return 44;

        if (baseName.Contains("medtile", StringComparison.OrdinalIgnoreCase) ||
            baseName.Contains("square150x150", StringComparison.OrdinalIgnoreCase))
            return 150;

        if (baseName.Contains("widetile", StringComparison.OrdinalIgnoreCase) ||
            baseName.Contains("wide310x150", StringComparison.OrdinalIgnoreCase))
            return 150;

        if (baseName.Contains("large", StringComparison.OrdinalIgnoreCase) ||
            baseName.Contains("square310x310", StringComparison.OrdinalIgnoreCase))
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
            var fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(path);

            var targetSize = TryGetQualifierNumber(TargetSizeRegex, fileNameWithoutExtension);
            var scale = TryGetQualifierNumber(ScaleRegex, fileNameWithoutExtension);

            var resolvedPixelSize = targetSize
                ?? (scale.HasValue ? logicalBaseSize * scale.Value / 100 : logicalBaseSize);

            var score = 0;
            score += resolvedPixelSize >= 48 ? 600 : 0;
            score += resolvedPixelSize >= 64 ? 350 : 0;
            score += resolvedPixelSize >= 96 ? 100 : 0;
            score -= Math.Abs(64 - resolvedPixelSize);

            if (targetSize.HasValue)
                score += 220;

            if (scale.HasValue)
                score += Math.Min(scale.Value, 400);

            if (includeThemeSpecific)
            {
                if (fileNameWithoutExtension.Contains("_altform-unplated", StringComparison.OrdinalIgnoreCase))
                    score += 120;
                if (fileNameWithoutExtension.Contains("_altform-lightunplated", StringComparison.OrdinalIgnoreCase))
                    score += 110;
            }

            if (fileNameWithoutExtension.Contains("storelogo", StringComparison.OrdinalIgnoreCase))
                score -= 300;

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
}
