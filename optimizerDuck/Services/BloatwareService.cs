using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Common.Helpers.Converters;
using optimizerDuck.Core.Models.Bloatware;
using optimizerDuck.Services.OptimizationServices;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace optimizerDuck.Services;

public class BloatwareService(ILogger<BloatwareService> logger)
{
    public async Task<List<AppXPackage>> GetAppXPackagesAsync()
    {
        try
        {
            var result = await ShellService.PowerShellAsync($$"""
                                                              # list the SAFE_APPS
                                                              $safeApps = @({{string.Join(", ", Shared.SafeApps.Keys.Select(x => $"\"{x}\""))}})

                                                              # list the CAUTION_APPS
                                                              $cautionApps = @({{string.Join(", ", Shared.CautionApps.Keys.Select(x => $"\"{x}\""))}})

                                                              # get installed removable apps
                                                              $installedApps = Get-AppxPackage -AllUsers | Where-Object { $_.NonRemovable -eq 0 }

                                                              $result = @()

                                                              foreach ($app in $installedApps) {

                                                                  $nameLower = $app.Name.ToLower()
                                                                  $risk = "Unknown"

                                                                  foreach ($s in $safeApps) {
                                                                      if ($nameLower -like "*$($s.ToLower())*") {
                                                                          $risk = "Safe"
                                                                          break
                                                                      }
                                                                  }

                                                                  if ($risk -eq "Unknown") {
                                                                      foreach ($s in $cautionApps) {
                                                                          if ($nameLower -like "*$($s.ToLower())*") {
                                                                              $risk = "Caution"
                                                                              break
                                                                          }
                                                                      }
                                                                  }

                                                                  if ($risk -ne "Unknown") {
                                                                      $result += [PSCustomObject]@{
                                                                          Name            = $app.Name
                                                                          PackageFullName = $app.PackageFullName
                                                                          Publisher       = $app.Publisher
                                                                          Version         = $app.Version
                                                                          InstallLocation = $app.InstallLocation
                                                                          Risk            = $risk
                                                                      }
                                                                  }
                                                              }

                                                              $result | ConvertTo-Json -Depth 4
                                                              """);


            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            var apps = JsonSerializer.Deserialize<List<AppXPackage>>(result.Stdout, options)!.OrderBy(a => a.Risk).ToList();

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

                           # remove for current + all users
                           Get-AppxPackage -AllUsers | Where-Object { $_.PackageFullName -eq $pkg } | ForEach-Object {
                               try {
                                   Remove-AppxPackage -Package $_.PackageFullName -AllUsers -ErrorAction Stop
                                   Write-Output "Removed installed package: $($_.PackageFullName)"
                               } catch {
                                   Write-Output "Failed removing installed package: $($_.PackageFullName)"
                               }
                           }

                           Write-Output "Removing provisioned package..."

                           # remove provisioned version so it doesn't reinstall
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