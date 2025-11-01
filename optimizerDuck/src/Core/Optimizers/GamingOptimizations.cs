using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Services;
using optimizerDuck.Interfaces;
using optimizerDuck.Models;
using optimizerDuck.UI.Logger;

namespace optimizerDuck.Core.Optimizers;

public class GamingOptimizations : IOptimizationGroup
{
    public string Name { get; } = "Gaming Optimizations";
    public int Priority { get; } = (int)OptimizationGroupPriority.GamingOptimizations;
    public static ILogger Log { get; } = Logger.CreateLogger<GamingOptimizations>();

    public class DisableGameBar : IOptimizationTweak
    {
        public string Name { get; } = "Disable Game Bar";

        public string Description { get; } =
            "Disables the Xbox Game Bar overlay and background services to prevent performance drops and improve in-game responsiveness.";

        public bool EnabledByDefault { get; } = true;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin();

            RegistryService.Write(
                new RegistryItem(@"HKCU\System\GameConfigStore", "GameBarEnabled", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\GameBar", "ShowStartupPanel", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\GameBar", "UseNexusForGameBarEnabled", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\GameBar", "GamePanelStartupTipIndex", 0)
            );
            Log.LogInformation("Game Bar has been disabled.");
            return Task.CompletedTask;
        }
    }

    public class DisableGameDVR : IOptimizationTweak
    {
        public string Name { get; } = "Disable Game DVR";

        public string Description { get; } =
            "Turns off Game DVR recording features to eliminate background video capture and reduce latency during gameplay.";

        public bool EnabledByDefault { get; } = true;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin();

            RegistryService.Write(
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0),
                new RegistryItem(@"HKCU\System\GameConfigStore", "GameDVR_Enabled", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0),
                new RegistryItem(@"HKLM\SOFTWARE\Microsoft\PolicyManager\default\ApplicationManagement\AllowGameDVR",
                    "value", 0)
            );
            Log.LogInformation("Game DVR has been disabled.");
            return Task.CompletedTask;
        }
    }

    public class GPUTweaks : IOptimizationTweak
    {
        public string Name { get; } = "GPU Tweaks";

        public string Description { get; } =
            "Applies GPU tweaks to disable extras and boost stability & performance";

        public bool EnabledByDefault { get; } = true;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin();
            Log.LogInformation("Applying GPU tweaks for {GpuCount} GPUs...", s.Gpus.Count);

            const string basePath =
                @"HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

            foreach (var gpu in s.Gpus)
            {
                Log.LogDebug("GPU Info: {GPUInfo}", gpu);
                if (string.IsNullOrWhiteSpace(gpu.DeviceId))
                {
                    Log.LogError("Skipping GPU with no Device ID: {GpuName} (Device ID: {DeviceId})", gpu.Name,
                        gpu.DeviceId ?? "null");
                    continue;
                }

                if (!TryParseDeviceIdToIndex(gpu.DeviceId, out var index))
                {
                    Log.LogError("Skipping GPU with invalid Device ID: {GpuName} (Device ID: {DeviceId})", gpu.Name,
                        gpu.DeviceId ?? "null");
                    continue;
                }

                var path = $"{basePath}\\{index:D4}";

                switch (gpu.Vendor)
                {
                    case GpuVendor.AMD:
                        RegistryService.Write(
                            new RegistryItem(path, "DisableDynamicPstate", 1),
                            new RegistryItem(path, "DisableVCEPowerGating", 1),
                            new RegistryItem(path, "EnableULPS", 0),
                            new RegistryItem(path, "EnableAspmL0s", 0),
                            new RegistryItem(path, "EnableAspmL1", 0),
                            new RegistryItem(path, "EnableUvdClockGating", 0),
                            new RegistryItem(path, "EnableVceSwClockGating", 0),
                            new RegistryItem(path, "KMD_EnableContextBasedPowerManagement", 0),
                            new RegistryItem(path, "PP_GPUPowerDownEnabled", 0),
                            new RegistryItem(path, "DisablePowerGating", 1),
                            new RegistryItem(path, "DisableVceClockGating", 1)
                        );
                        Log.LogInformation("Applied AMD GPU tweaks for {GpuName} at index {GpuIndex:D4}.", gpu.Name,
                            index);
                        break;
                    case GpuVendor.NVIDIA:
                        RegistryService.Write(
                            new RegistryItem(path, "DisableDynamicPstate", 1),
                            new RegistryItem(path, "DisableASyncPstates", 1)
                        );
                        Log.LogInformation("Applied NVIDIA GPU tweaks for {GpuName} at index {GpuIndex:D4}.", gpu.Name,
                            index);
                        break;
                    case GpuVendor.Intel:
                        RegistryService.Write(
                            new RegistryItem(path, "Disable_OverlayDSQualityEnhancement", 1),
                            new RegistryItem(path, "IncreaseFixedSegment", 1),
                            new RegistryItem(path, "AdaptiveVsyncEnable", 0),
                            new RegistryItem(path, "DisablePFonDP", 1),
                            new RegistryItem(path, "EnableCompensationForDVI", 1),
                            new RegistryItem(path, "Display1_DisableAsyncFlips", 1)
                        );
                        Log.LogInformation("Applied Intel GPU tweaks for {GpuName} at index {GpuIndex:D4}.", gpu.Name,
                            index);
                        break;
                    case GpuVendor.Unknown:
                    default:
                        Log.LogError("Unsupported GPU vendor: {GpuVendor} for {GpuName} at index {GpuIndex:D4}.",
                            gpu.Vendor, gpu.Name, index);
                        break;
                }
            }

            return Task.CompletedTask;
        }


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
        private static bool TryParseDeviceIdToIndex(string deviceId, out int index)
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
    }
}