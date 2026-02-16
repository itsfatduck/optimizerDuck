using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Common.Helpers.Converters;
using optimizerDuck.Core.Models.Bloatware;
using optimizerDuck.Core.Models.Config;
using optimizerDuck.Services.OptimizationServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using System.Text.RegularExpressions;
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

                Get-AppxPackage | Where-Object { $_.NonRemovable -eq $false } | ForEach-Object {
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

                           Write-Output "Searching installed package..."

                           $installed = Get-AppxPackage -AllUsers | Where-Object { $_.PackageFullName -eq $pkgFull }

                           if (-not $installed) {
                               Write-Output "Installed package not found"
                           }
                           else {
                               foreach ($p in $installed) {
                                   try {
                                       Remove-AppxPackage -AllUsers -Package $p.PackageFullName -ErrorAction Stop
                                       Write-Output "Removed installed package: $($p.PackageFullName)"
                                   }
                                   catch {
                                       Write-Output "Failed removing installed package: $($p.PackageFullName)"
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
                                      Write-Output "Failed removing provisioned package: $($p.PackageName)"
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
}