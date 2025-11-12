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
using System.Reflection;

namespace optimizerDuck.Core.Helpers;

public static class OptimizationHelper
{
    private static readonly ILogger Log = Logger.CreateLogger(typeof(OptimizationHelper));

    public static List<OptimizationGroupChoice> GetTweakChoices()
    {
        return CacheManager.GetOrCreate("optimization_choices", entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;

            var groups = new List<OptimizationGroupChoice>();
            var optimizationGroups = ReflectionHelper.FindImplementationsInLoadedAssemblies(typeof(IOptimizationGroup));

            var enumerable = optimizationGroups as Type[] ?? optimizationGroups.ToArray();
            var allTweaks = enumerable
                .SelectMany(og => og.GetNestedTypes(BindingFlags.Public)
                    .Where(t => typeof(IOptimizationTweak).IsAssignableFrom(t))
                    .Select(t => (IOptimizationTweak?)Activator.CreateInstance(t)!))
                .Where(_ => true)
                .ToList();

            var globalMaxNameLength = allTweaks.Count != 0
                ? allTweaks.Max(t => t.Name.Length) + 1
                : 0;
            var maxImpactLength = allTweaks.Count != 0
                ? allTweaks.Max(t => t.Impact.GetDescription().Length) + 1
                : 0;


            foreach (var optimizationGroup in enumerable)
            {
                var groupInstance = (IOptimizationGroup)Activator.CreateInstance(optimizationGroup)!;

                var tweaks = optimizationGroup
                    .GetNestedTypes(BindingFlags.Public)
                    .Where(t => typeof(IOptimizationTweak).IsAssignableFrom(t))
                    .Select(t => (IOptimizationTweak)Activator.CreateInstance(t)!)
                    .OrderByDescending(t => t.EnabledByDefault)
                    .ToList();

                if (tweaks.Count == 0) // skip groups with no tweaks
                    continue;

                var tweakChoices = tweaks
                    .Select(t =>
                    {
                        var paddedName = t.Name.PadRight(globalMaxNameLength);
                        var description = $"{t.Impact.GetDescription().PadRight(maxImpactLength)}[dim]{t.Description}[/]";

                        return new OptimizationTweakChoice(t, paddedName, description, t.EnabledByDefault);
                    })
                    .ToList();

                groups.Add(new OptimizationGroupChoice(groupInstance.Name, groupInstance.Order, tweakChoices));
            }

            var orderedGroups = groups.OrderBy(g => g.Priority).ToList();
            foreach (var g in orderedGroups)
            {
                Log.LogDebug("Loaded group {Group}:", g.Name);
                foreach (var t in g.Tweaks) Log.LogDebug("  - {TweakName} {TweakDescription}", t.Name, Markup.Remove(t.Description));
            }

            return orderedGroups;
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
                            DisplayName = Defaults.SAFE_APPS.TryGetValue(app.Name, out var dn) ? dn : app.Name // get display name or fallback to app name
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