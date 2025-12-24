using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using optimizerDuck.Core.Extensions;
using optimizerDuck.Core.Managers;
using optimizerDuck.Core.Services;
using optimizerDuck.Interfaces;
using optimizerDuck.Models;
using optimizerDuck.UI;
using optimizerDuck.UI.Logger;
using Spectre.Console;

namespace optimizerDuck.Core.Helpers;

public static class OptimizationHelper
{
    private static readonly ILogger Log = Logger.CreateLogger(typeof(OptimizationHelper));

    public static List<OptimizationCategoryChoice> LoadOptimizationChoices()
    {
        return CacheManager.GetOrCreate("optimization_choices", entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;

            var optimizationCategories = ReflectionHelper
                .FindImplementationsInLoadedAssemblies(typeof(IOptimizationCategory))
                .ToArray();

            var allOptimizationsByCategory = optimizationCategories
                .Select(g => new
                {
                    Category = (IOptimizationCategory)Activator.CreateInstance(g)!,
                    Optimizations = g.GetNestedTypes(BindingFlags.Public)
                        .Where(t => typeof(IOptimization).IsAssignableFrom(t))
                        .Select(t => (IOptimization)Activator.CreateInstance(t)!)
                        .ToList()
                })
                .ToList();

            var allOptimizations = allOptimizationsByCategory.SelectMany(x => x.Optimizations).ToList();
            var globalMaxNameLength = allOptimizations.DefaultIfEmpty().Max(t => t?.Name?.Length ?? 0) + 1;
            var maxImpactLength =
                allOptimizations.DefaultIfEmpty().Max(t => t?.Impact.GetDescription().Length ?? 0) + 1;

            var categories = new List<OptimizationCategoryChoice>();

            foreach (var g in allOptimizationsByCategory)
            {
                var optimizations = g.Optimizations
                    .OrderByDescending(t => t.EnabledByDefault)
                    .ThenBy(t => (int)t.Impact)
                    .ToList();

                if (optimizations.Count == 0) continue; // Skip empty categories

                var optimizationChoices = optimizations.Select(t =>
                {
                    var paddedName = t.Name.PadRight(globalMaxNameLength);
                    var description = $"{t.Impact.GetDescription().PadRight(maxImpactLength)}[dim]{t.Description}[/]";
                    return new OptimizationChoice(t, paddedName, description, t.EnabledByDefault);
                }).ToList();

                categories.Add(new OptimizationCategoryChoice(g.Category.Name, g.Category.Order, optimizationChoices));
            }

            var orderedCategories = categories.OrderBy(g => g.Order).ToList();

            foreach (var g in orderedCategories)
            {
                Log.LogDebug("Loaded category [{Category}]:", g.Name);
                foreach (var t in g.Optimizations)
                    Log.LogDebug("  - {OptimizationName} {OptimizationDescription}", t.Name,
                        Markup.Remove(t.Description));
            }

            return orderedCategories;
        });
    }

    public static AppxClassification GetBloatwareChoices()
    {
        try
        {
            var result = ShellService.PowerShell($$"""
                                                   # list the SAFE_APPS
                                                   $safeApps = @({{string.Join(", ", Defaults.SAFE_APPS.Keys.Select(x => $"\"{x}\""))}})

                                                   # list the CAUTION_APPS
                                                   $cautionApps = @({{string.Join(", ", Defaults.CAUTION_APPS.Keys.Select(x => $"\"{x}\""))}})

                                                   # get the installed apps
                                                   $installedApps = Get-AppxPackage -AllUsers | Where-Object { $_.NonRemovable -eq 0 } # NonRemovable = 0 means the app can be removed

                                                   # categorize the installed apps
                                                   $safeInstalled = $installedApps | Where-Object {
                                                       foreach ($s in $safeApps) {
                                                           if ($_.Name.ToLower() -like "*$($s.ToLower())*") { return $true }
                                                       }
                                                       return $false
                                                   } | Select-Object Name, Version, InstallLocation

                                                   $cautionInstalled = $installedApps | Where-Object {
                                                       foreach ($s in $cautionApps) {
                                                           if ($_.Name.ToLower() -like "*$($s.ToLower())*") { return $true }
                                                       }
                                                       return $false
                                                   } | Select-Object Name, Version, InstallLocation

                                                   # create the JSON object
                                                   $result = [PSCustomObject]@{
                                                       SafeApps    = @($safeInstalled | ForEach-Object {
                                                           [PSCustomObject]@{
                                                               Name            = $_.Name
                                                               Version         = $_.Version
                                                               InstallLocation = $_.InstallLocation
                                                           }
                                                       })
                                                       CautionApps = @($cautionInstalled | ForEach-Object {
                                                           [PSCustomObject]@{
                                                               Name            = $_.Name
                                                               Version         = $_.Version
                                                               InstallLocation = $_.InstallLocation
                                                           }
                                                       })
                                                   }

                                                   # output JSON
                                                   $result | ConvertTo-Json -Depth 4
                                                   """);


            var raw = JsonConvert.DeserializeObject<AppxClassificationRaw>(result.Stdout);

            var allSafe = raw.SafeApps.SelectMany(x => x).ToList();
            var allCaution = raw.CautionApps.SelectMany(x => x).ToList();

            // Display Length
            var safeDisplayLength = allSafe.Count != 0
                ? allSafe.Max(x => Defaults.SAFE_APPS.TryGetValue(x.Name, out var dn) ? dn.Length : x.Name.Length)
                : 0;

            var cautionDisplayLength = allCaution.Count != 0
                ? allCaution.Max(x => Defaults.CAUTION_APPS.TryGetValue(x.Name, out var dn) ? dn.Length : x.Name.Length)
                : 0;

            // Version Length
            var safeVersionLength = allSafe.Count != 0
                ? allSafe.Max(x => x.Version.Length)
                : 0;

            var cautionVersionLength = allCaution.Count != 0
                ? allCaution.Max(x => x.Version.Length)
                : 0;


            var maxDisplayNameLength = Math.Max(safeDisplayLength, cautionDisplayLength) + 1;

            var maxVersionLength = Math.Max(safeVersionLength, cautionVersionLength) + 1;


            var classification = new AppxClassification(
                allSafe
                    .Select(app =>
                    {
                        return app with
                        {
                            DisplayName =
                            Defaults.SAFE_APPS.TryGetValue(app.Name, out var dn)
                                ? dn
                                : app.Name // get display name or fallback to app name
                        };
                    })
                    .OrderBy(x => x.DisplayName)
                    .Select(app => app with
                    {
                        DisplayName = app.DisplayName.PadRight(maxDisplayNameLength),
                        Version = app.Version.PadRight(maxVersionLength)
                    })
                    .ToList(),
                allCaution
                    .Select(app =>
                    {
                        return app with
                        {
                            DisplayName = Defaults.CAUTION_APPS.TryGetValue(app.Name, out var dn) ? dn : app.Name
                        };
                    })
                    .OrderBy(x => x.DisplayName)
                    .Select(app => app with
                    {
                        DisplayName = app.DisplayName.PadRight(maxDisplayNameLength),
                        Version = app.Version.PadRight(maxVersionLength)
                    })
                    .ToList()
            );

            var parts = new List<string>();

            if (classification.SafeApps.Count > 0)
            {
                parts.Add($"[{Theme.Success}]{classification.SafeApps.Count}[/] safe apps");
                Log.LogDebug("Found {TotalSafeApps} safe apps: {SafeApps}", classification.SafeApps.Count,
                    string.Join(", ", classification.SafeApps.Select(x => x.Name).ToList()));
            }

            if (classification.CautionApps.Count > 0)
            {
                parts.Add($"[{Theme.Error}]{classification.CautionApps.Count}[/] caution apps");
                Log.LogDebug("Found {TotalCautionApps} caution apps: {CautionApps}", classification.CautionApps.Count,
                    string.Join(", ", classification.CautionApps.Select(x => x.Name).ToList()));
            }

            if (parts.Count != 0)
                Log.LogInformation($"Found {string.Join(" and ", parts)}.");
            else
                Log.LogWarning("No apps found.");

            return classification;
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "Failed to get bloatware choices.");
            return new AppxClassification(new List<AppxPackage>(), new List<AppxPackage>());
        }
    }
}