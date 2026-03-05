using Newtonsoft.Json.Linq;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.OptimizationServices;

namespace optimizerDuck.Core.Models.Revert.Steps;

/// <summary>
///     Represents a revert step that restores a Windows service to its original startup type.
/// </summary>
public class ServiceRevertStep : IRevertStep
{
    /// <summary>
    ///     The name of the Windows service to restore.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    ///     The original startup type to restore the service to.
    /// </summary>
    public ServiceStartupType OriginalStartupType { get; set; }

    /// <inheritdoc />
    public string Type => "Service";

    /// <inheritdoc />
    public string Description => string.Format(
        Translations.Revert_Service_Description_Restore,
        ServiceName, OriginalStartupType);


    /// <inheritdoc />
    public async Task<bool> ExecuteAsync()
    {
        return await Task.Run(() =>
        {
            var result = ServiceProcessService.ChangeServiceStartupType(new ServiceItem
            {
                Name = ServiceName,
                StartupType = OriginalStartupType
            });

            if (!result)
                throw new Exception(string.Format(Translations.Service_Service_Error_UpdateRegistryForStartupTypeFailed));

            return result;
        });
    }


    /// <inheritdoc />
    public JObject ToData()
    {
        return new JObject
        {
            [nameof(ServiceName)] = ServiceName,
            [nameof(OriginalStartupType)] = OriginalStartupType.ToString()
        };
    }


    /// <summary>
    ///     Deserializes a <see cref="ServiceRevertStep" /> from JSON data.
    /// </summary>
    /// <param name="data">The JSON data to deserialize.</param>
    /// <returns>A new <see cref="ServiceRevertStep" /> instance.</returns>
    public static ServiceRevertStep FromData(JObject data)
    {
        return new ServiceRevertStep
        {
            ServiceName = data[nameof(ServiceName)]?.ToString() ?? string.Empty,
            OriginalStartupType = Enum.Parse<ServiceStartupType>(
                data[nameof(OriginalStartupType)]?.ToString() ?? "Manual")
        };
    }
}