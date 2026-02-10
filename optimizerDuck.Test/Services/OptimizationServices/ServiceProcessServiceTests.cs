using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Services.OptimizationServices;

namespace optimizerDuck.Test.Services.OptimizationServices;

public class ServiceProcessServiceTests
{
    [Fact]
    public void ChangeServiceStartupType_WithMissingService_ReturnsFalse()
    {
        var item = new ServiceItem
        {
            Name = "optimizerDuck-Test-NonExistent",
            StartupType = ServiceStartupType.Disabled
        };

        var result = ServiceProcessService.ChangeServiceStartupType(item);

        Assert.False(result);
    }
}