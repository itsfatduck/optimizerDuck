using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using optimizerDuck.Core.Helpers;
using optimizerDuck.Core.Services;
using optimizerDuck.Interfaces;
using optimizerDuck.Models;
using optimizerDuck.UI.Logger;

namespace optimizerDuck.Core.Optimizers;

public class Performance : IOptimizationGroup
{
    public string Name => "Performance";
    public int Order => (int)OptimizationGroupOrder.Performance;
    public static ILogger Log { get; } = Logger.CreateLogger<Performance>();

    public class BackgroundAppsTweak : IOptimizationTweak
    {
        public string Name { get; } = "Disable Background Apps";
        public string Description { get; } = "Stops background applications from running to free up RAM and CPU resources";
        public bool EnabledByDefault { get; } = true;
        public OptimizationImpact Impact { get; } = OptimizationImpact.Minimal;


        public Task Apply(SystemSnapshot s)
        {
            RegistryService.Write(
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 0)
            );
            Log.LogInformation("Disabled background applications.");
            return Task.CompletedTask;
        }
    }

    public class ProcessPriorityTweak : IOptimizationTweak
    {
        public string Name { get; } = "Optimize Process Priority";
        public string Description { get; } = "Adjusts foreground app priority for better responsiveness and reduced input lag";
        public bool EnabledByDefault { get; } = true;
        public OptimizationImpact Impact { get; } = OptimizationImpact.Significant;


        public Task Apply(SystemSnapshot s)
        {
            /*
             ref: https://forums.blurbusters.com/viewtopic.php?t=8535
            42 Dec, 2A Hex = Short, Fixed , High foreground boost.
            41 Dec, 29 Hex = Short, Fixed , Medium foreground boost.
            40 Dec, 28 Hex = Short, Fixed , No foreground boost.

            38 Dec, 26 Hex = Short, Variable , High foreground boost.
            37 Dec, 25 Hex = Short, Variable , Medium foreground boost.
            36 Dec, 24 Hex = Short, Variable , No foreground boost.

            26 Dec, 1A Hex = Long, Fixed, High foreground boost.
            25 Dec, 19 Hex = Long, Fixed, Medium foreground boost.
            24 Dec, 18 Hex = Long, Fixed, No foreground boost.

            22 Dec, 16 Hex = Long, Variable, High foreground boost.
            21 Dec, 15 Hex = Long, Variable, Medium foreground boost.
            20 Dec, 14 Hex = Long, Variable, No foreground boost.
             */

            const int win32Priority = 38; // Short, Variable, High foreground boost
            RegistryService.Write(
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", win32Priority)
            );
            Log.LogInformation("Applied process priority tweak (Win32PrioritySeparation = {Value}).", win32Priority);
            return Task.CompletedTask;
        }
    }

    public class GameSchedulingTweak : IOptimizationTweak
    {
        public string Name { get; } = "Optimize Gaming Scheduling";
        public string Description { get; } = "Prioritizes GPU scheduling and system resources for gaming workloads";
        public bool EnabledByDefault { get; } = true;
        public OptimizationImpact Impact { get; } = OptimizationImpact.Moderate;

        public Task Apply(SystemSnapshot s)
        {
            RegistryService.Write(
                new RegistryItem(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Priority", 2),
                new RegistryItem(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Scheduling Category", "High"),
                new RegistryItem(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "SFIO Priority", "High"),
                new RegistryItem(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority", 8),
                new RegistryItem(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord),
                new RegistryItem(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 10),
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2)
            );
            Log.LogInformation("Optimized gaming scheduling and priority.");
            return Task.CompletedTask;
        }
    }

    public class SvcHostSplitTweak : IOptimizationTweak
    {
        public string Name { get; } = "SvcHost Split Threshold";
        public string Description { get; } = "Adjusts SvcHostSplitThresholdInKB based on total system RAM to control service isolation and improve system stability.";
        public bool EnabledByDefault { get; } = true;
        public OptimizationImpact Impact { get; } = OptimizationImpact.Significant;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin(Log);

            if (s.Ram.TotalKB <= 0)
            {
                Log.LogInformation("Invalid RAM value: {RamTotalKB}. Skipping...", s.Ram.TotalKB);
                return Task.CompletedTask;
            }
            RegistryService.Write(
                new RegistryItem(@"HKLM\SYSTEM\CurrentControlSet\Control", "SvcHostSplitThresholdInKB", s.Ram.TotalKB, RegistryValueKind.DWord)
            );
            Log.LogInformation("Set SvcHostSplitThresholdInKB to {Value}.", s.Ram.TotalKB);
            return Task.CompletedTask;
        }
    }




    public class DisableGameBar : IOptimizationTweak
    {
        public string Name { get; } = "Disable Game Bar";

        public string Description { get; } =
            "Disables the Xbox Game Bar overlay and background services to prevent performance drops and improve in-game responsiveness";

        public bool EnabledByDefault { get; } = true;
        public OptimizationImpact Impact { get; } = OptimizationImpact.Minimal;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin(Log);

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

    public class GameModeTweak : IOptimizationTweak
    {
        public string Name { get; } = "Enable Game Mode";
        public string Description { get; } = "Enables Windows Game Mode (Recommended on Windows 11)";
        public bool EnabledByDefault { get; } = SystemHelper.IsWindows11OrGreater();
        public OptimizationImpact Impact { get; } = OptimizationImpact.Minimal;

        public Task Apply(SystemSnapshot s)
        {
            RegistryService.Write(
                new RegistryItem(@"HKCU\Software\Microsoft\GameBar", "AllowAutoGameMode", 1),
                new RegistryItem(@"HKCU\Software\Microsoft\GameBar", "AutoGameModeEnabled", 1)
            );
            Log.LogInformation("Enabled Game Mode.");

            return Task.CompletedTask;
        }
    }

    public class DisableGameDVR : IOptimizationTweak
    {
        public string Name { get; } = "Disable Game DVR";

        public string Description { get; } =
            "Turns off Game DVR recording features to eliminate background video capture and reduce latency during gameplay";

        public bool EnabledByDefault { get; } = true;
        public OptimizationImpact Impact { get; } = OptimizationImpact.Minimal;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin(Log);

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

    public class GpuOptimizationTweak : IOptimizationTweak
    {
        public string Name { get; } = "GPU Optimization";

        public string Description { get; } = "Optimizes GPU driver settings to disable unnecessary features and improve stability & performance";

        public bool EnabledByDefault { get; } = true;
        public OptimizationImpact Impact { get; } = OptimizationImpact.Significant;

        public Task Apply(SystemSnapshot s)
        {
            using var tracker = ServiceTracker.Begin(Log);
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