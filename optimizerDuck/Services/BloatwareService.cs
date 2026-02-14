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
    public async Task<List<AppxPackage>> GetAppXPackagesAsync()
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
                Converters = { new JsonStringEnumConverter()}
            };

            var apps = JsonSerializer.Deserialize<List<AppxPackage>>(result.Stdout, options);

            logger.LogInformation("Found {AppCount} AppX packages", apps.Count);

            return apps;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get bloatware choices");
            return [];
        }
    }
}