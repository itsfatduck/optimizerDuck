using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Common.Helpers.Converters;
using optimizerDuck.Core.Models.Bloatware;
using optimizerDuck.Core.Models.Config;
using optimizerDuck.Services.OptimizationServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace optimizerDuck.Services;

public class BloatwareService(ILogger<BloatwareService> logger, IOptionsMonitor<AppSettings> appOptionsMonitor)
{
    public async Task<List<AppXPackage>> GetAppXPackagesAsync()
    {
        try
        {
            var result = await ShellService.PowerShellAsync($$"""
                                                               # SAFE_APPS
                                                               $safeApps = "{{string.Join(",", Shared.SafeApps.Keys)}}" -split "," |
                                                                           ForEach-Object { $_.Trim().ToLower() }

                                                               # CAUTION_APPS
                                                               $cautionApps = "{{string.Join(",", Shared.CautionApps.Keys)}}" -split "," |
                                                                              ForEach-Object { $_.Trim().ToLower() }

                                                               # Installed removable apps
                                                               $installedApps = Get-AppxPackage | Where-Object { $_.NonRemovable -eq 0 }

                                                               # Build result array efficiently
                                                               $result = foreach ($app in $installedApps) {
                                                                   $nameLower = $app.Name.ToLower()
                                                                   $risk = "Unknown"

                                                                   if ($safeApps | Where-Object { $nameLower -like "*$_*" }) {
                                                                       $risk = "Safe"
                                                                   }
                                                                   elseif ($cautionApps | Where-Object { $nameLower -like "*$_*" }) {
                                                                       $risk = "Caution"
                                                                   }

                                                                   [PSCustomObject]@{
                                                                       Name            = $app.Name
                                                                       PackageFullName = $app.PackageFullName
                                                                       Publisher       = $app.Publisher
                                                                       Version         = $app.Version.ToString()
                                                                       InstallLocation = if ($app.InstallLocation) { $app.InstallLocation } else { "" }
                                                                       Risk            = $risk
                                                                   }
                                                               }

                                                               @($result) | ConvertTo-Json -Depth 4

                                                               """);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            var apps = JsonSerializer.Deserialize<List<AppXPackage>>(result.Stdout, options)!.OrderBy(a => a.Name).ToList();

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
                        $pkg = "{{appXPackage.PackageFullName}}"

                        Write-Output "Removing installed package..."

                        Get-AppxPackage -AllUsers |
                        Where-Object { $_.PackageFullName -eq $pkg } |
                        ForEach-Object {
                            try {
                                Remove-AppxPackage -Package $_.PackageFullName -AllUsers -ErrorAction Stop
                                Write-Output "Removed installed package: $($_.PackageFullName)"
                            } catch {
                                Write-Output "Failed removing installed package: $($_.PackageFullName)"
                            }
                        }
                        """;

            if (appOptionsMonitor.CurrentValue.Bloatware.RemoveProvisioned)
            {
                script += """

                            Write-Output "Removing provisioned package..."

                            Get-AppxProvisionedPackage -Online |
                            Where-Object { $_.PackageName -like "*$pkg*" } |
                            ForEach-Object {
                                try {
                                    Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction Stop | Out-Null
                                    Write-Output "Removed provisioned package: $($_.PackageName)"
                                } catch {
                                    Write-Output "Failed removing provisioned package: $($_.PackageName)"
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