using System.Management;
using Microsoft.Extensions.Logging;
using optimizerDuck.Models;
using optimizerDuck.UI.Logger;

namespace optimizerDuck.Core.Services;

public static class ServiceProcessService
{
    private static readonly ManagementScope Scope = new(@"\\.\root\cimv2");

    public static bool ChangeServiceStartupType(ServiceItem item)
    {
        try
        {
            if (!Scope.IsConnected)
                Scope.Connect();

            using var searcher = new ManagementObjectSearcher(Scope,
                new ObjectQuery($"SELECT * FROM Win32_Service WHERE Name='{item.ServiceName.Replace("'", "''")}'"));
            var service = searcher.Get().OfType<ManagementObject>().FirstOrDefault();

            if (service == null)
            {
                ServiceTracker.Current?.Log.LogError("Service {ServiceName} not found.", item.ServiceName);
                ServiceTracker.Current?.Track(nameof(ChangeServiceStartupType), false);
                return false;
            }

            using var inParams = service.GetMethodParameters("ChangeStartMode");
            inParams["StartMode"] = item.StartupType switch
            {
                ServiceStartupType.AutomaticDelayedStart => "Automatic",
                _ => item.StartupType.ToString()
            };

            using var outParams = service.InvokeMethod("ChangeStartMode", inParams, null);

            var resultCode = outParams?["ReturnValue"] is uint val ? val : 1;

            if (resultCode == 0)
            {
                var result = item.StartupType == ServiceStartupType.AutomaticDelayedStart
                    ? RegistryService.Write(new RegistryItem(
                        @$"HKLM\SYSTEM\CurrentControlSet\Services\{item.ServiceName}", "DelayedAutoStart", 1))
                    : RegistryService.DeleteValue(new RegistryItem(
                        @$"HKLM\SYSTEM\CurrentControlSet\Services\{item.ServiceName}", "DelayedAutoStart"));

                ServiceTracker.Current?.Track(nameof(ChangeServiceStartupType), result);
                return result;
            }

            ServiceTracker.Current?.Log.LogError(
                "Failed to change startup type for {ServiceName}. ReturnValue: {Result}", item.ServiceName,
                resultCode);
            ServiceTracker.Current?.Track(nameof(ChangeServiceStartupType), false);
            return false;
        }
        catch (Exception ex)
        {
            ServiceTracker.Current?.Log.LogError(ex, "Failed to change startup type for {ServiceName}",
                item.ServiceName);
            ServiceTracker.Current?.Track(nameof(ChangeServiceStartupType), false);
            return false;
        }
    }

    public static void ChangeServiceStartupType(params ServiceItem[] items)
    {
        foreach (var item in items) ChangeServiceStartupType(item);
    }
}