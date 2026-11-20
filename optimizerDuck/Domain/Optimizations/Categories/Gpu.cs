using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Conditions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.UI.Pages.Optimize.Categories;

namespace optimizerDuck.Domain.Optimizations.Categories;

public abstract class GpuRegistryOptimization : BaseOptimization
{
    protected abstract GpuVendor Vendor { get; }

    protected abstract IReadOnlyList<RegistryItem> CreateItems(string registryPath);

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

            RegistryService.Write(context, CreateItems(path).ToArray());
            context.Logger.LogInformation(
                "Applied {Optimization} for GPU {GpuName} at {Path}",
                GetType().Name,
                gpu.Name,
                path
            );
        }

        return Task.FromResult(context.Changes.ToApplyResult());
    }
}

[OptimizationCategory(typeof(GpuOptimizerPage))]
public class Gpu : LocalizedObject, IOptimizationCategory
{
    public string Name => Loc.Instance[$"Optimizer.{nameof(Gpu)}"];
    public OptimizationCategoryOrder Order { get; init; } = OptimizationCategoryOrder.Gpu;
    public ObservableCollection<IOptimization> Optimizations { get; init; } = [];

    /// <summary>The Display adapter device setup class GUID.</summary>
    internal const string DisplayClassGuid = "{4d36e968-e325-11ce-bfc1-08002be10318}";

    /// <summary>
    ///     Resolves the Display Class registry key that belongs to one adapter, for example
    ///     <c>HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-...}\0001</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The key comes from the <c>Driver</c> value under
    ///         <c>HKLM\SYSTEM\CurrentControlSet\Enum\&lt;PnpDeviceId&gt;</c>, which is the link
    ///         Windows itself keeps between a device instance and its class key.
    ///     </para>
    ///     <para>
    ///         The WMI <c>DeviceID</c> (<c>VideoControllerN</c>) must not be used for this: its
    ///         numbering follows WMI enumeration order, which need not match the order of the
    ///         class subkeys. On hybrid-GPU laptops, or with a virtual display adapter installed,
    ///         it points at another adapter's key.
    ///     </para>
    /// </remarks>
    /// <param name="pnpDeviceId">The adapter's PnP device instance ID.</param>
    /// <param name="readDriverValue">
    ///     Reads the <c>Driver</c> value for a PnP device instance ID; returns
    ///     <see langword="null" /> when absent or unreadable.
    /// </param>
    /// <param name="path">On success, the full class key path; otherwise empty.</param>
    /// <returns>
    ///     <see langword="true" /> only when the <c>Driver</c> value names a Display Class
    ///     subkey; anything else is rejected rather than guessed.
    /// </returns>
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

        // Expected form: "{4d36e968-e325-11ce-bfc1-08002be10318}\0001".
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
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Amd | OptimizationTags.Power,
        Condition = typeof(AmdGpuCondition)
    )]
    public class AmdDisableUlps : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Amd;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return [new RegistryItem(path, "EnableULPS", 0)];
        }
    }

    [Optimization(
        Id = "A3836587-C461-4BEB-9C4C-88493B36C138",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Amd | OptimizationTags.Performance,
        Condition = typeof(AmdGpuCondition)
    )]
    public class AmdDisablePowerGating : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Amd;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return
            [
                new RegistryItem(path, "DisablePowerGating", 1),
                new RegistryItem(path, "PP_GPUPowerDownEnabled", 0),
                new RegistryItem(path, "DisableDynamicPstate", 1),
            ];
        }
    }

    [Optimization(
        Id = "DB379BB5-FC8C-4AD3-BE32-11BE09E76FBA",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Amd | OptimizationTags.Performance,
        Condition = typeof(AmdGpuCondition)
    )]
    public class AmdDisableVideoClockGating : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Amd;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return
            [
                new RegistryItem(path, "DisableVCEPowerGating", 1),
                new RegistryItem(path, "DisableVceClockGating", 1),
                new RegistryItem(path, "EnableUvdClockGating", 0),
                new RegistryItem(path, "EnableVceSwClockGating", 0),
            ];
        }
    }

    [Optimization(
        Id = "8A4B4C4D-000E-42DB-8BD7-293067F8FAE7",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Amd | OptimizationTags.Latency,
        Condition = typeof(AmdGpuCondition)
    )]
    public class AmdDisableAspm : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Amd;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return
            [
                new RegistryItem(path, "EnableAspmL0s", 0),
                new RegistryItem(path, "EnableAspmL1", 0),
            ];
        }
    }

    [Optimization(
        Id = "FA58802F-B98C-4E98-AA67-067C5A394296",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Nvidia | OptimizationTags.Power,
        Condition = typeof(NvidiaGpuCondition)
    )]
    public class NvidiaDisableDynamicPstate : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Nvidia;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return [new RegistryItem(path, "DisableDynamicPstate", 1)];
        }
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

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return [new RegistryItem(path, "DisableASyncPstates", 1)];
        }
    }

    [Optimization(
        Id = "DF8739FF-E322-4958-B369-DF584BB9B0C1",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Intel | OptimizationTags.Latency,
        Condition = typeof(IntelGpuCondition)
    )]
    public class IntelDisableAsyncFlips : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Intel;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return [new RegistryItem(path, "Display1_DisableAsyncFlips", 1)];
        }
    }

    [Optimization(
        Id = "EBFB0DED-5D1A-4CD1-B5C0-4F7018DD6054",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Intel | OptimizationTags.Display,
        Condition = typeof(IntelGpuCondition)
    )]
    public class IntelDisableAdaptiveVsync : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Intel;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return [new RegistryItem(path, "AdaptiveVsyncEnable", 0)];
        }
    }
}
