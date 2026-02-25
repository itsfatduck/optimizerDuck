using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Models.Bloatware;
using optimizerDuck.Core.Models.Config;
using optimizerDuck.Services.OptimizationServices;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace optimizerDuck.Services;

public class BloatwareService(ILogger<BloatwareService> logger, IOptionsMonitor<AppSettings> appOptionsMonitor)
{
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
                    {
                        app.LogoImage = manifestLogo;
                        continue;
                    }
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

    public async Task RemoveAppXPackage(AppXPackage appXPackage)
    {
        using var tracker = ServiceTracker.Begin(logger);
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
            {
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
            }
            else
            {
                script += """
                          Write-Output "Skipping provisioned package removal (disabled by user)"
                          """;
            }

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
        try
        {
            // 1. Fast path: check for common high-quality logos first without parsing manifest
            var assetsDir = Path.Combine(installLocation, "Assets");
            if (Directory.Exists(assetsDir))
            {
                var commonLogos = new[] { "Logo.png" };
                foreach (var cl in commonLogos)
                {
                    var path = Path.Combine(assetsDir, cl);
                    if (File.Exists(path)) return path;
                }

                // If exact matches aren't found, try patterns that might have scale modifiers
                var highQualityPatterns = new[] { "*StoreLogo*.png", "*Logo*.png" };
                foreach (var pattern in highQualityPatterns)
                {
                    var match = Directory.EnumerateFiles(assetsDir, pattern, SearchOption.TopDirectoryOnly)
                                         .OrderByDescending(f => new FileInfo(f).Length)
                                         .FirstOrDefault();
                    if (match != null) return match;
                }
            }

            // 2. Fallback: Parse AppxManifest.xml
            var manifest = Path.Combine(installLocation, "AppxManifest.xml");
            if (!File.Exists(manifest)) return null;

            var doc = XDocument.Load(manifest);
            var root = doc.Root;
            if (root == null) return null;

            XNamespace ns = root.Name.Namespace;
            List<string> candidates = [];

            var props = root.Element(ns + "Properties");
            if (props != null)
            {
                var logo = props.Element(ns + "Logo")?.Value;
                if (!string.IsNullOrWhiteSpace(logo)) candidates.Add(logo);
            }

            var visual = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "VisualElements" || e.Name.LocalName == "DefaultTile");
            if (visual != null)
            {
                AddAttr(candidates, visual, "Square150x150Logo");
                AddAttr(candidates, visual, "Wide310x150Logo");
                AddAttr(candidates, visual, "Square44x44Logo");
            }

            // Resolve manifest candidates
            foreach (var rel in candidates)
            {
                var clean = rel.Replace('/', '\\');
                var full = Path.Combine(installLocation, clean);

                if (File.Exists(full)) return full;

                // Fallback: search for file with matching name and extension but different scale modifiers
                var nameWithoutExt = Path.GetFileNameWithoutExtension(clean);
                var ext = Path.GetExtension(clean);
                var dirPath = Path.GetDirectoryName(clean);
                var searchDir = string.IsNullOrEmpty(dirPath) ? installLocation : Path.Combine(installLocation, dirPath);

                if (Directory.Exists(searchDir))
                {
                    var found = Directory.EnumerateFiles(searchDir, $"{nameWithoutExt}*{ext}", SearchOption.TopDirectoryOnly)
                                         .OrderByDescending(f => new FileInfo(f).Length)
                                         .FirstOrDefault();
                    if (found != null) return found;
                }
            }
        }
        catch
        {
            // Ignored: returning null if any file access/parsing fails
        }

        return null;
    }

    private static void AddAttr(List<string> list, XElement el, string name)
    {
        var val = el.Attribute(name)?.Value;
        if (!string.IsNullOrWhiteSpace(val)) list.Add(val);
    }
}