using System.Diagnostics;
using System.Management;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Core.Models.Revert.Steps;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Managers;

namespace optimizerDuck.Services.OptimizationServices;

public static class ServiceProcessService
{
    private static readonly ManagementScope Scope = new(@"\\.\root\cimv2");

    /// <summary>
    ///     Get current startup type of a service
    /// </summary>
    private static ServiceStartupType? GetStartupType(string serviceName, ManagementObject service)
    {
        try
        {
            var startMode = service["StartMode"]?.ToString();

            if (startMode is "Automatic" or "Auto")
            {
                var isDelayed = RegistryService.Read<int>(new RegistryItem(
                    $@"HKLM\SYSTEM\CurrentControlSet\Services\{serviceName}", "DelayedAutoStart"));
                if (isDelayed == 1)
                    return ServiceStartupType.AutomaticDelayedStart;
            }

            return startMode switch
            {
                "Auto" or "Automatic" => ServiceStartupType.Automatic,
                "Manual" => ServiceStartupType.Manual,
                "Disabled" => ServiceStartupType.Disabled,
                _ => null
            };
        }
        catch (Exception ex)
        {
            ServiceTracker.LogError(ex, "Failed to get startup type for {ServiceName}", serviceName);
            return null;
        }
    }

    public static bool ChangeServiceStartupType(ServiceItem item)
    {
        var description = $"{item.Name} -> {item.StartupType}";
        try
        {
            if (!Scope.IsConnected)
                Scope.Connect();

            using var searcher = new ManagementObjectSearcher(Scope,
                new ObjectQuery($"SELECT * FROM Win32_Service WHERE Name='{item.Name.Replace("'", "''")}'"));

            using var results = searcher.Get();
            using var service = results.OfType<ManagementObject>().FirstOrDefault();

            if (service == null)
            {
                ServiceTracker.LogInfo(
                    "[SERVICE][{Name}][FAIL][NOT_FOUND][D=0ms] startup -> {StartupType}",
                    item.Name,
                    item.StartupType
                );
                ServiceTracker.Track(nameof(ChangeServiceStartupType), false);
                ServiceTracker.TrackStep(
                    "Service",
                    description,
                    false,
                    Translations.Service_Service_Error_NotFound,
                    () => Task.Run(() => ChangeServiceStartupType(item)));
                return false;
            }

            // Record revert: backup current startup type
            var originalStartupType = GetStartupType(item.Name, service);

            var sw = Stopwatch.StartNew();

            using var inParams = service.GetMethodParameters("ChangeStartMode");
            inParams["StartMode"] = item.StartupType switch
            {
                ServiceStartupType.Automatic or ServiceStartupType.AutomaticDelayedStart => "Automatic",
                ServiceStartupType.Manual => "Manual",
                ServiceStartupType.Disabled => "Disabled",
                _ => "Manual"
            };

            using var outParams = service.InvokeMethod("ChangeStartMode", inParams, null);
            var resultCode = outParams?["ReturnValue"] is uint val ? val : 1;

            if (resultCode == 0)
            {
                // Handle delayed auto start registry key
                var registrySuccess = item.StartupType == ServiceStartupType.AutomaticDelayedStart
                    ? RegistryService.Write(new RegistryItem(
                        $@"HKLM\SYSTEM\CurrentControlSet\Services\{item.Name}",
                        "DelayedAutoStart", 1))
                    // always delete to ensure consistency
                    : RegistryService.DeleteValue(new RegistryItem(
                        $@"HKLM\SYSTEM\CurrentControlSet\Services\{item.Name}",
                        "DelayedAutoStart"));

                if (registrySuccess && originalStartupType.HasValue)
                    RevertManager.Record(new ServiceRevertStep
                    {
                        ServiceName = item.Name,
                        OriginalStartupType = originalStartupType.Value
                    });

                sw.Stop();

                ServiceTracker.LogInfo(
                    "[SERVICE][{Name}][OK][CODE=0][D={Duration}] startup -> {StartupType}",
                    item.Name,
                    sw.Elapsed.FormatTime(),
                    item.StartupType
                );

                ServiceTracker.Track(nameof(ChangeServiceStartupType), registrySuccess);
                ServiceTracker.TrackStep(
                    "Service",
                    description,
                    registrySuccess,
                    registrySuccess ? null : Translations.Service_Service_Error_UpdateRegistryForStartupTypeFailed,
                    () => Task.Run(() => ChangeServiceStartupType(item)));
                return registrySuccess;
            }

            ServiceTracker.LogInfo(
                "[SERVICE][{Name}][FAIL][CODE={Code}][D={Duration}] startup -> {StartupType}",
                item.Name,
                resultCode,
                sw.Elapsed.FormatTime(),
                item.StartupType
            );

            ServiceTracker.Track(nameof(ChangeServiceStartupType), false);
            ServiceTracker.TrackStep(
                "Service",
                description,
                false,
                string.Format(Translations.Service_Service_Error_ChangeStartModeFailedWithCode, resultCode),
                () => Task.Run(() => ChangeServiceStartupType(item)));
            return false;
        }
        catch (Exception ex)
        {
            ServiceTracker.LogError(
                ex,
                "[SERVICE][{Name}][FAIL][EXCEPTION] startup -> {StartupType}",
                item.Name,
                item.StartupType
            );
            ServiceTracker.Track(nameof(ChangeServiceStartupType), false);
            ServiceTracker.TrackStep(
                "Service",
                description,
                false,
                ex.Message,
                () => Task.Run(() => ChangeServiceStartupType(item)));
            return false;
        }
    }

    public static void ChangeServiceStartupType(params ServiceItem[] items)
    {
        foreach (var item in items)
            ChangeServiceStartupType(item);
    }
}