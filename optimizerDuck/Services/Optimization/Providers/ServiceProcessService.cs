using System.Diagnostics;
using System.Management;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Resources.Languages;

namespace optimizerDuck.Services.Optimization.Providers;

public static class ServiceProcessService
{
    private static readonly AsyncLocal<string?> _lastError = new();
    private static readonly AsyncLocal<string?> _lastErrorDetail = new();

    internal static string? LastError => _lastError.Value;
    internal static string? LastErrorDetail => _lastErrorDetail.Value;

    private static readonly ManagementScope Scope = new(@"\\.\root\cimv2");

    /// <summary>Gets the current startup type of a Windows service from the registry.</summary>
    /// <param name="serviceName">The name of the service.</param>
    /// <returns>The startup type, or <see langword="null" /> if the value is unrecognized or an error occurs.</returns>
    public static ServiceStartupType? GetStartupType(string serviceName)
    {
        try
        {
            var serviceKey = $@"HKLM\SYSTEM\CurrentControlSet\Services\{serviceName}";
            var start = RegistryService.Read<int>(new RegistryItem(serviceKey, "Start"));

            if (start == 2)
            {
                var isDelayed = RegistryService.Read<int>(
                    new RegistryItem(serviceKey, "DelayedAutoStart")
                );
                return isDelayed == 1
                    ? ServiceStartupType.AutomaticDelayedStart
                    : ServiceStartupType.Automatic;
            }

            return start switch
            {
                3 => ServiceStartupType.Manual,
                4 => ServiceStartupType.Disabled,
                _ => null,
            };
        }
        catch (Exception ex)
        {
            ExecutionScope.LogError(
                ex,
                "Failed to get startup type for {ServiceName}",
                serviceName
            );
            return null;
        }
    }

    /// <summary>Changes a service's startup type by writing the corresponding registry values.</summary>
    /// <param name="item">The service name and target startup type.</param>
    /// <returns><see langword="true" /> if the change succeeded; otherwise, <see langword="false" />.</returns>
    public static bool ChangeServiceStartupType(ServiceItem item)
    {
        _lastError.Value = _lastErrorDetail.Value = null;

        var description = string.Format(
            Translations.Service_Service_Description_Change,
            item.Name,
            item.StartupType
        );
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
                _ => 3,
            };

            var serviceKey = $@"HKLM\SYSTEM\CurrentControlSet\Services\{item.Name}";

            var mainSuccess = RegistryService.Write(
                new RegistryItem(serviceKey, "Start", startValue)
            );
            var regError = RegistryService.LastError;
            var regErrorDetail = RegistryService.LastErrorDetail;

            // handle 'DelayedAutoStart' if necessary
            var delayedSuccess = true;
            if (item.StartupType == ServiceStartupType.AutomaticDelayedStart)
                delayedSuccess = RegistryService.Write(
                    new RegistryItem(serviceKey, "DelayedAutoStart", 1)
                );
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
                        OriginalStartupType = originalStartupType.Value,
                    };

                ExecutionScope.LogInfo(
                    "[SERVICE][{Name}][OK][D={Duration}] startup -> {StartupType}",
                    item.Name,
                    sw.Elapsed.FormatTime(),
                    item.StartupType
                );

                ExecutionScope.Track(nameof(ChangeServiceStartupType), true);
                ExecutionScope.RecordStep(
                    Translations.Service_Service_Name,
                    description,
                    true,
                    revertStep
                );
                return true;
            }

            _lastError.Value =
                regError ?? Translations.Service_Service_Error_UpdateRegistryForStartupTypeFailed;
            _lastErrorDetail.Value = regErrorDetail;
            ExecutionScope.LogInfo(
                "[SERVICE][{Name}][FAIL][D={Duration}] startup -> {StartupType}",
                item.Name,
                sw.Elapsed.FormatTime(),
                item.StartupType
            );
            ExecutionScope.Track(nameof(ChangeServiceStartupType), false);
            ExecutionScope.RecordStep(
                Translations.Service_Service_Name,
                description,
                false,
                null,
                _lastError.Value,
                () => Task.FromResult(ChangeServiceStartupType(item)),
                _lastErrorDetail.Value
            );
            return false;
        }
        catch (Exception ex)
        {
            _lastError.Value = string.Format(
                Translations.Service_Service_Error_ExceptionOccurred,
                item.Name,
                ex.Message
            );
            _lastErrorDetail.Value = ex.ToString();
            ExecutionScope.LogError(
                ex,
                "[SERVICE][{Name}][FAIL][EXCEPTION] startup -> {StartupType}",
                item.Name,
                item.StartupType
            );
            ExecutionScope.Track(nameof(ChangeServiceStartupType), false);
            ExecutionScope.RecordStep(
                Translations.Service_Service_Name,
                description,
                false,
                null,
                _lastError.Value,
                () => Task.FromResult(ChangeServiceStartupType(item)),
                _lastErrorDetail.Value
            );
            return false;
        }
    }

    /// <summary>Changes the startup type for multiple services.</summary>
    /// <param name="items">The service items to update.</param>
    public static void ChangeServiceStartupType(params ServiceItem[] items)
    {
        foreach (var item in items)
            ChangeServiceStartupType(item);
    }
}
