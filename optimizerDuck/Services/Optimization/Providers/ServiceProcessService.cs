using System.Diagnostics;
using System.Management;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Resources.Languages;

namespace optimizerDuck.Services.OptimizationServices;

public static class ServiceProcessService
{
    private static readonly ManagementScope Scope = new(@"\\.\root\cimv2");

    /// <summary>
    ///     Get current startup type of a service
    /// </summary>
    private static ServiceStartupType? GetStartupType(string serviceName)
    {
        try
        {
            var serviceKey = $@"HKLM\SYSTEM\CurrentControlSet\Services\{serviceName}";
            var start = RegistryService.Read<int>(new RegistryItem(serviceKey, "Start"));

            if (start == 2)
            {
                var isDelayed = RegistryService.Read<int>(new RegistryItem(serviceKey, "DelayedAutoStart"));
                return isDelayed == 1 ? ServiceStartupType.AutomaticDelayedStart : ServiceStartupType.Automatic;
            }

            return start switch
            {
                3 => ServiceStartupType.Manual,
                4 => ServiceStartupType.Disabled,
                _ => null
            };
        }
        catch (Exception ex)
        {
            ExecutionScope.LogError(ex, "Failed to get startup type for {ServiceName}", serviceName);
            return null;
        }
    }

    public static bool ChangeServiceStartupType(ServiceItem item)
    {
        var description = string.Format(Translations.Service_Service_Description_Change, item.Name, item.StartupType);
        var sw = Stopwatch.StartNew();

        try
        {
            var originalStartupType = GetStartupType(item.Name);

            // Map ServiceStartupType to Registry 'Start' value
            var startValue = item.StartupType switch
            {
                ServiceStartupType.Automatic or ServiceStartupType.AutomaticDelayedStart => 2,
                ServiceStartupType.Manual => 3,
                ServiceStartupType.Disabled => 4,
                _ => 3
            };

            var serviceKey = $@"HKLM\SYSTEM\CurrentControlSet\Services\{item.Name}";

            // 1. Change the main 'Start' value
            var mainSuccess = RegistryService.Write(new RegistryItem(serviceKey, "Start", startValue));

            // 2. Handle 'DelayedAutoStart' if necessary
            var delayedSuccess = true;
            if (item.StartupType == ServiceStartupType.AutomaticDelayedStart)
                delayedSuccess = RegistryService.Write(new RegistryItem(serviceKey, "DelayedAutoStart", 1));
            else
                // Always try to delete for consistency, but don't fail if it doesn't exist
                RegistryService.DeleteValue(new RegistryItem(serviceKey, "DelayedAutoStart"));

            var success = mainSuccess && delayedSuccess;
            sw.Stop();

            if (success)
            {
                ServiceRevertStep? revertStep = null;
                if (originalStartupType.HasValue && originalStartupType.Value != item.StartupType)
                    revertStep = new ServiceRevertStep
                    {
                        ServiceName = item.Name,
                        OriginalStartupType = originalStartupType.Value
                    };

                ExecutionScope.LogInfo(
                    "[SERVICE][{Name}][OK][D={Duration}] startup -> {StartupType}",
                    item.Name,
                    sw.Elapsed.FormatTime(),
                    item.StartupType
                );

                ExecutionScope.Track(nameof(ChangeServiceStartupType), true);
                ExecutionScope.RecordStep(Translations.Service_Service_Name, description, true, revertStep);
                return true;
            }

            ExecutionScope.LogInfo("[SERVICE][{Name}][FAIL][D={Duration}] startup -> {StartupType}", item.Name,
                sw.Elapsed.FormatTime(), item.StartupType);
            ExecutionScope.Track(nameof(ChangeServiceStartupType), false);
            ExecutionScope.RecordStep(Translations.Service_Service_Name, description, false, null,
                Translations.Service_Service_Error_UpdateRegistryForStartupTypeFailed,
                () => Task.FromResult(ChangeServiceStartupType(item)));
            return false;
        }
        catch (Exception ex)
        {
            ExecutionScope.LogError(ex, "[SERVICE][{Name}][FAIL][EXCEPTION] startup -> {StartupType}", item.Name,
                item.StartupType);
            ExecutionScope.Track(nameof(ChangeServiceStartupType), false);
            ExecutionScope.RecordStep(Translations.Service_Service_Name, description, false, null,
                string.Format(Translations.Service_Service_Error_ExceptionOccurred, item.Name, ex.Message),
                () => Task.FromResult(ChangeServiceStartupType(item)));
            return false;
        }
    }

    public static void ChangeServiceStartupType(params ServiceItem[] items)
    {
        foreach (var item in items)
            ChangeServiceStartupType(item);
    }
}