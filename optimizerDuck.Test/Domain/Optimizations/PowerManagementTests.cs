using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Test.Domain.Optimizations;

public class PowerManagementTests
{
    [Fact]
    public void RegistryPath_WithHklmPrefix_IsReadable()
    {
        var call = new OpCall { Logger = NullLogger.Instance };
        var key = $@"HKCU\SOFTWARE\OptimizerDuckTest\{Guid.NewGuid():N}";

        try
        {
            Assert.True(RegistryService.Write(call, new RegistryItem(key, "Probe", 1)).Ok);
            Assert.Equal(1, RegistryService.Read<int>(new RegistryItem(key, "Probe")));
        }
        finally
        {
            RegistryService.DeleteSubKeyTree(call, new RegistryItem(key));
        }
    }
}
