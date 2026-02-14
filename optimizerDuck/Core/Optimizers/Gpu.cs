using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.Optimization;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Services;
using optimizerDuck.Services.Managers;
using optimizerDuck.Services.OptimizationServices;
using optimizerDuck.UI.Views.Pages.Optimizations;

namespace optimizerDuck.Core.Optimizers;

public abstract class GpuRegistryOptimization : BaseOptimization
{
    protected abstract GpuVendor Vendor { get; }
    protected abstract IReadOnlyList<RegistryItem> CreateItems(string registryPath);

    public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
    {
        foreach (var gpu in context.Snapshot.Gpus
                     .Where(g => g.Vendor == Vendor && !string.IsNullOrEmpty(g.DeviceId)))
        {
            if (!Gpu.TryParseDeviceIdToIndex(gpu.DeviceId, out var index))
            {
                context.Logger.LogWarning("Invalid DeviceId for GPU {GpuName} ({DeviceId})", gpu.Name, gpu.DeviceId);
                continue;
            }

            var path =
                $@"HKLM\SYSTEM\CurrentControlSet\Control\Class\{{4d36e968-e325-11ce-bfc1-08002be10318}}\{index:D4}";

            RegistryService.Write(CreateItems(path).ToArray());
            context.Logger.LogInformation("Applied {Optimization} for GPU {DeviceId}", GetType().Name, gpu.DeviceId);
        }

        return Task.FromResult(ApplyResult.True());
    }
}

[OptimizationCategory(typeof(GpuOptimizerPage))]
public class Gpu : IOptimizationCategory
{
    public string Name { get; init; } = Loc.Instance[$"Optimizer.{nameof(Gpu)}"];
    public OptimizationCategoryOrder Order { get; init; } = OptimizationCategoryOrder.Gpu;
    public ObservableCollection<IOptimization> Optimizations { get; init; } = [];


    /// <summary>
    ///     Attempts to parse a WMI <c>DeviceID</c> (e.g., "VideoController1") into a zero-based registry index
    ///     used in the Display Class registry path (e.g., "0000", "0001").
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Windows stores display adapter settings under:
    ///         <c>HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\XXXX</c>
    ///         where <c>XXXX</c> is a 4-digit zero-padded index (e.g., <c>0000</c>).
    ///     </para>
    ///     <para>
    ///         The <see cref="Win32_VideoController" /> WMI class provides a <c>DeviceID</c> property in the format:
    ///         <c>"VideoController1"</c>, <c>"VideoController2"</c>, etc. This method converts:
    ///         <list type="bullet">
    ///             <item>
    ///                 <description><c>"VideoController1"</c> → index <c>0</c> → registry subkey <c>"0000"</c></description>
    ///             </item>
    ///             <item>
    ///                 <description><c>"VideoController2"</c> → index <c>1</c> → registry subkey <c>"0001"</c></description>
    ///             </item>
    ///         </list>
    ///     </para>
    ///     <para>
    ///         This mapping is <strong>required</strong> because the order of GPUs in <see cref="SystemSnapshot.Gpus" />
    ///         does <strong>not</strong> guarantee alignment with registry index order. Using array index (0, 1, 2...)
    ///         leads to applying tweaks to the wrong GPU (e.g., iGPU instead of dGPU).
    ///     </para>
    /// </remarks>
    /// <param name="deviceId">
    ///     The <c>DeviceID</c> string from <c>Win32_VideoController</c>, typically in the form
    ///     <c>"VideoControllerN"</c> where <c>N</c> is a positive integer starting from 1.
    /// </param>
    /// <param name="index">
    ///     When the method returns <c>true</c>, contains the zero-based index suitable for formatting with
    ///     <c>:D4</c> (e.g., <c>0</c> → <c>"0000"</c>). When <c>false</c>, the value is <c>-1</c>.
    /// </param>
    /// <returns>
    ///     <c>true</c> if <paramref name="deviceId" /> is valid and successfully parsed; otherwise, <c>false</c>.
    /// </returns>
    /// <example>
    ///     <code>
    /// string deviceId = "VideoController1";
    /// if (TryParseDeviceIdToIndex(deviceId, out int index))
    /// {
    ///     string regPath = $@"HKLM\...\{{4d36e968-...\}}\{index:D4}"; // → "0000"
    ///     Console.WriteLine($"Apply tweak to GPU at registry index: {index:D4}");
    /// }
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException">
    ///     Thrown if <paramref name="deviceId" /> is <c>null</c> — but this method uses <c>string.IsNullOrWhiteSpace</c>
    ///     and returns <c>false</c> instead for consistency with TryParse pattern.
    /// </exception>
    /// <seealso cref="GpuInfo.DeviceId" />
    /// <seealso href="https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-videocontroller">
    ///     Win32_VideoController WMI class
    /// </seealso>
    public static bool TryParseDeviceIdToIndex(string? deviceId, out int index)
    {
        index = -1;
        if (string.IsNullOrWhiteSpace(deviceId))
            return false;

        const string prefix = "VideoController";
        if (!deviceId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var numberPart = deviceId[prefix.Length..];
        if (!int.TryParse(numberPart, out var deviceNumber) || deviceNumber <= 0)
            return false;

        index = deviceNumber - 1; // convert to zero-based index
        return true;
    }

    [Optimization(
        Id = "4433CE93-B632-4D67-AE71-0241C31099C0",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Amd | OptimizationTags.Power)]
    public class AmdDisableUlps : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.AMD;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return
            [
                new(path, "EnableULPS", 0)
            ];
        }
    }

    [Optimization(
        Id = "A3836587-C461-4BEB-9C4C-88493B36C138",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Amd | OptimizationTags.Performance)]
    public class AmdDisablePowerGating : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.AMD;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return
            [
                new(path, "DisablePowerGating", 1),
                new(path, "PP_GPUPowerDownEnabled", 0),
                new(path, "DisableDynamicPstate", 1)
            ];
        }
    }

    [Optimization(
        Id = "DB379BB5-FC8C-4AD3-BE32-11BE09E76FBA",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Amd | OptimizationTags.Performance)]
    public class AmdDisableVideoClockGating : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.AMD;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return
            [
                new(path, "DisableVCEPowerGating", 1),
                new(path, "DisableVceClockGating", 1),
                new(path, "EnableUvdClockGating", 0),
                new(path, "EnableVceSwClockGating", 0)
            ];
        }
    }


    [Optimization(
        Id = "8A4B4C4D-000E-42DB-8BD7-293067F8FAE7",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Amd | OptimizationTags.Latency)]
    public class AmdDisableAspm : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.AMD;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return
            [
                new(path, "EnableAspmL0s", 0),
                new(path, "EnableAspmL1", 0)
            ];
        }
    }


    [Optimization(
        Id = "FA58802F-B98C-4E98-AA67-067C5A394296",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Nvidia | OptimizationTags.Power)]
    public class NvidiaDisableDynamicPstate : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.NVIDIA;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return
            [
                new(path, "DisableDynamicPstate", 1)
            ];
        }
    }


    [Optimization(
        Id = "9E7702BE-9E03-4C96-A316-C4E435CB993E",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Nvidia | OptimizationTags.Performance)]
    public class NvidiaDisableAsyncPstates : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.NVIDIA;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return
            [
                new(path, "DisableASyncPstates", 1)
            ];
        }
    }

    [Optimization(
        Id = "DF8739FF-E322-4958-B369-DF584BB9B0C1",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Intel | OptimizationTags.Latency)]
    public class IntelDisableAsyncFlips : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Intel;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return
            [
                new(path, "Display1_DisableAsyncFlips", 1)
            ];
        }
    }


    [Optimization(
        Id = "EBFB0DED-5D1A-4CD1-B5C0-4F7018DD6054",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Intel | OptimizationTags.Display)]
    public class IntelDisableAdaptiveVsync : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Intel;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return
            [
                new(path, "AdaptiveVsyncEnable", 0)
            ];
        }
    }
}