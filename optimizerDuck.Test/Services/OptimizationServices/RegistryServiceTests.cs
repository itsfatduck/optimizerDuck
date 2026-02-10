using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Services.OptimizationServices;

namespace optimizerDuck.Test.Services.OptimizationServices;

public class RegistryServiceTests
{
    [Fact]
    public void Read_WithInvalidRoot_ReturnsDefault()
    {
        var item = new RegistryItem("BADROOT\\SomePath", "ValueName");

        var result = RegistryService.Read<string>(item);

        Assert.Null(result);
    }

    [Fact]
    public void DeleteValue_WithInvalidRoot_ReturnsFalse()
    {
        var item = new RegistryItem("BADROOT\\SomePath", "ValueName");

        var result = RegistryService.DeleteValue(item);

        Assert.False(result);
    }

    [Fact]
    public void Write_WithNullValue_ReturnsFalse()
    {
        var item = new RegistryItem("HKCU\\Software\\optimizerDuck.Test", "TestValue");

        var result = RegistryService.Write(item);

        Assert.False(result);
    }
}