using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Services.OptimizationServices;

namespace optimizerDuck.Test.Domain.Optimizations;

public class PowerManagementTests
{
    [Fact]
    public void RegistryPath_WithHklmPrefix_IsReadable()
    {
        using var scope = ExecutionScope.BeginForLogging(NullLogger.Instance);
        var key = $@"HKCU\SOFTWARE\OptimizerDuckTest\{Guid.NewGuid():N}";

        try
        {
            Assert.True(RegistryService.Write(new RegistryItem(key, "Probe", 1)));
            Assert.Equal(1, RegistryService.Read<int>(new RegistryItem(key, "Probe")));
        }
        finally
        {
            RegistryService.DeleteSubKeyTree(new RegistryItem(key));
        }
    }
}
