using Newtonsoft.Json.Linq;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Services.OptimizationServices;

namespace optimizerDuck.Core.Models.Revert.Steps;

public class ServiceRevertStep : IRevertStep
{
    public string ServiceName { get; set; } = string.Empty;
    public ServiceStartupType OriginalStartupType { get; set; }
    public string Type => "Service";

    public async Task<bool> ExecuteAsync()
    {
        return await Task.Run(() => ServiceProcessService.ChangeServiceStartupType(new ServiceItem
        {
            Name = ServiceName,
            StartupType = OriginalStartupType
        }));
    }

    public JObject ToData()
    {
        return new JObject
        {
            [nameof(ServiceName)] = ServiceName,
            [nameof(OriginalStartupType)] = OriginalStartupType.ToString()
        };
    }

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