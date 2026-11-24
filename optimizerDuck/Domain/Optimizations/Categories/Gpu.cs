using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Conditions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.UI.Pages.Optimize.Categories;

namespace optimizerDuck.Domain.Optimizations.Categories;

/// <summary>
///     Shared base for GPU registry tweaks that resolve each matching adapter's hardware display
///     class key (<c>0000</c>, <c>0001</c>, ...) at runtime before reading or writing registry values.
/// </summary>
public abstract class GpuRegistryOptimization : BaseOptimization
{
    protected abstract GpuVendor Vendor { get; }

    protected abstract IReadOnlyList<RegistryItem> CreateItems(string registryPath);

    public override Task<bool?> IsSystemAtTargetAsync(
        SystemInfo snapshot,
        IReadOnlyDictionary<string, string>? appliedOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        var items = new List<RegistryItem>();
        foreach (var gpu in snapshot.Gpus.Where(g => g.Vendor == Vendor))
        {
            if (
                !Gpu.TryResolveClassKeyPath(
                    gpu.PnpDeviceId,
                    pnpDeviceId => Gpu.ReadDriverValue(pnpDeviceId, null),
                    out var path
                )
            )
                return Task.FromResult<bool?>(null);

            items.AddRange(CreateItems(path));
        }

        return items.Count == 0
            ? Task.FromResult<bool?>(null)
            : Task.FromResult(StateProbe.RegistryMatches([.. items]));
    }

    public override Task<ApplyResult> ApplyAsync(
        IProgress<ProcessingProgress> progress,
        OptimizationContext context
    )
    {
        foreach (var gpu in context.Snapshot.Gpus.Where(g => g.Vendor == Vendor))
        {
            if (
                !Gpu.TryResolveClassKeyPath(
                    gpu.PnpDeviceId,
                    pnpDeviceId => Gpu.ReadDriverValue(pnpDeviceId, context.Logger),
                    out var path
                )
            )
            {
                // Never guess: a wrong guess writes vendor tweaks onto another adapter.
                context.Logger.LogWarning(
                    "Could not resolve the display class key for GPU {GpuName} ({PnpDeviceId}), skipping",
                    gpu.Name,
                    gpu.PnpDeviceId
                );
                continue;
            }

            RegistryService.Write(context, [.. CreateItems(path)]);
            context.Logger.LogInformation(
                "Applied {Optimization} for GPU {GpuName} at {Path}",
                OptimizationKey,
                gpu.Name,
                path
            );
        }

        return Task.FromResult(context.Changes.ToApplyResult());
    }
}

[OptimizationCategory(typeof(GpuOptimizerPage))]
public class Gpu() : OptimizationCategoryBase(OptimizationCategoryOrder.Gpu)
{
    /// <summary>The Display adapter device setup class GUID.</summary>
    internal const string DisplayClassGuid = "{4d36e968-e325-11ce-bfc1-08002be10318}";

    /// <summary>
    ///     Resolves the Display Class registry key that belongs to one adapter, for example
    ///     <c>HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-...}\0001</c>.
    /// </summary>
    public static bool TryResolveClassKeyPath(
        string? pnpDeviceId,
        Func<string, string?> readDriverValue,
        out string path
    )
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(pnpDeviceId))
            return false;

        var driver = readDriverValue(pnpDeviceId)?.Trim();
        if (string.IsNullOrEmpty(driver))
            return false;

        var separator = driver.IndexOf('\\');
        if (separator < 0)
            return false;

        var classGuid = driver[..separator];
        var instance = driver[(separator + 1)..];
        if (
            !string.Equals(classGuid, DisplayClassGuid, StringComparison.OrdinalIgnoreCase)
            || instance.Length != 4
            || !instance.All(char.IsAsciiDigit)
        )
            return false;

        path = $@"HKLM\SYSTEM\CurrentControlSet\Control\Class\{DisplayClassGuid}\{instance}";
        return true;
    }

    /// <summary>Reads the <c>Driver</c> value of a PnP device instance from the registry.</summary>
    internal static string? ReadDriverValue(string pnpDeviceId, ILogger? logger) =>
        RegistryService.TryReadValue(
            new RegistryItem($@"HKLM\SYSTEM\CurrentControlSet\Enum\{pnpDeviceId}", "Driver"),
            out var value,
            logger
        )
            ? value as string
            : null;

    [Optimization(
        Id = "4433CE93-B632-4D67-AE71-0241C31099C0",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Amd | OptimizationTags.Power,
        Condition = typeof(AmdGpuCondition)
    )]
    public class AmdDisableUlps : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Amd;

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Stability),
                    Improves(ImpactArea.Latency),
                    Costs(ImpactArea.Power),
                ],
                Sources =
                [
                    Source(
                        "AMD Graphics Power Saving Registry Options - KernelOS",
                        "https://kernelos.org/docs/en/post-install/graphics/graphics_amd/"
                    ),
                    Source(
                        "WDDM GPU Power Management of Idle States - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows-hardware/drivers/display/gpu-power-management-of-idle-and-active-power"
                    ),
                ],
                RequiresRestart = RestartRequirement.Reboot,
            };

        protected override IReadOnlyList<RegistryItem> CreateItems(string path) =>
            [new RegistryItem(path, "EnableULPS", 0)];
    }

    [Optimization(
        Id = "A3836587-C461-4BEB-9C4C-88493B36C138",
        Risk = OptimizationRisk.Risky,
        Tags = OptimizationTags.Amd | OptimizationTags.Performance,
        Condition = typeof(AmdGpuCondition)
    )]
    public class AmdDisablePowerGating : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Amd;

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Latency),
                    Improves(ImpactArea.Performance),
                    Costs(ImpactArea.Power),
                ],
                Sources =
                [
                    Source(
                        "AMD Graphics Power Saving Registry Options - KernelOS",
                        "https://kernelos.org/docs/en/post-install/graphics/graphics_amd/"
                    ),
                    Source(
                        "WDDM GPU Power Management of Idle States - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows-hardware/drivers/display/gpu-power-management-of-idle-and-active-power"
                    ),
                ],
                RequiresRestart = RestartRequirement.Reboot,
            };

        protected override IReadOnlyList<RegistryItem> CreateItems(string path) =>
            [
                new RegistryItem(path, "DisablePowerGating", 1),
                new RegistryItem(path, "PP_GPUPowerDownEnabled", 0),
                new RegistryItem(path, "DisableDynamicPstate", 1),
            ];
    }

    [Optimization(
        Id = "8A4B4C4D-000E-42DB-8BD7-293067F8FAE7",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Amd | OptimizationTags.Latency,
        Condition = typeof(AmdGpuCondition)
    )]
    public class AmdDisableAspm : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Amd;

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Latency), Costs(ImpactArea.Power)],
                Sources =
                [
                    Source(
                        "AMD Graphics Power Saving Registry Options - KernelOS",
                        "https://kernelos.org/docs/en/post-install/graphics/graphics_amd/"
                    ),
                    Source(
                        "WDDM GPU Power Management of Idle States - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows-hardware/drivers/display/gpu-power-management-of-idle-and-active-power"
                    ),
                ],
                RequiresRestart = RestartRequirement.Reboot,
            };

        protected override IReadOnlyList<RegistryItem> CreateItems(string path) =>
            [new RegistryItem(path, "EnableAspmL0s", 0), new RegistryItem(path, "EnableAspmL1", 0)];
    }

    [Optimization(
        Id = "FA58802F-B98C-4E98-AA67-067C5A394296",
        Risk = OptimizationRisk.Risky,
        Tags = OptimizationTags.Nvidia | OptimizationTags.Power,
        Condition = typeof(NvidiaGpuCondition)
    )]
    public class NvidiaDisableDynamicPstate : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Nvidia;

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Latency), Costs(ImpactArea.Power)],
                Sources =
                [
                    Source(
                        "WDDM GPU Power Management of Idle States - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows-hardware/drivers/display/gpu-power-management-of-idle-and-active-power"
                    ),
                ],
                RequiresRestart = RestartRequirement.Reboot,
            };

        protected override IReadOnlyList<RegistryItem> CreateItems(string path) =>
            [new RegistryItem(path, "DisableDynamicPstate", 1)];
    }

    [Optimization(
        Id = "9E7702BE-9E03-4C96-A316-C4E435CB993E",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Nvidia | OptimizationTags.Performance,
        Condition = typeof(NvidiaGpuCondition)
    )]
    public class NvidiaDisableAsyncPstates : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Nvidia;

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Latency), Costs(ImpactArea.Power)],
                Sources =
                [
                    Source(
                        "WDDM GPU Power Management of Idle States - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows-hardware/drivers/display/gpu-power-management-of-idle-and-active-power"
                    ),
                ],
                RequiresRestart = RestartRequirement.Reboot,
            };

        protected override IReadOnlyList<RegistryItem> CreateItems(string path) =>
            [new RegistryItem(path, "DisableASyncPstates", 1)];
    }

    [Optimization(
        Id = "EBFB0DED-5D1A-4CD1-B5C0-4F7018DD6054",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Intel | OptimizationTags.Display,
        Condition = typeof(IntelGpuCondition)
    )]
    public class IntelDisableAdaptiveVsync : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Intel;

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Latency), Costs(ImpactArea.Features)],
                Sources =
                [
                    Source(
                        "Enabling and Disabling Vertical Synchronization - Intel Support",
                        "https://www.intel.com/content/www/us/en/support/articles/000005552/graphics.html"
                    ),
                    Source(
                        "Intel Adaptive Sync with 11th-Gen Graphics & DX12 - Intel Developer",
                        "https://www.intel.com/content/www/us/en/developer/articles/technical/enabling-intel-adaptive-sync-with-11th-generation-intel-processor-graphics-and-microsoft.html"
                    ),
                ],
                RequiresRestart = RestartRequirement.Reboot,
            };

        protected override IReadOnlyList<RegistryItem> CreateItems(string path) =>
            [new RegistryItem(path, "AdaptiveVsyncEnable", 0)];
    }
}
