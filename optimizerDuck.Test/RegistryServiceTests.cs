using Microsoft.Win32;
using optimizerDuck.Core.Services;
using optimizerDuck.Models;

namespace optimizerDuck.Test;

public class RegistryServiceTests
{
    [Fact]
    public void Read()
    {
        var pathString =
            RegistryService.Read<string>(new RegistryItem(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost",
                "Path"));
        var versionString =
            RegistryService.Read<string>(new RegistryItem(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost",
                "Version"));

        var binary =
            RegistryService.Read<byte[]>(new RegistryItem(@"HKEY_LOCAL_MACHINE\SOFTWARE\Logitech\LGHUB\Data",
                "canary_machine_identifier"));

        Assert.NotNull(pathString);
        Assert.NotNull(versionString);
        Assert.NotNull(binary);
    }

    [Fact]
    public void Write_And_Read_Back_String_Value()
    {
        var testKeyPath = @"HKEY_CURRENT_USER\Software\optimizerDuckTests";
        var item = new RegistryItem(testKeyPath, "TestString", "hello", RegistryValueKind.String);
        try
        {
            var written = RegistryService.Write(item);
            Assert.True(written);

            var readBack = RegistryService.Read<string>(new RegistryItem(testKeyPath, "TestString"));
            Assert.Equal("hello", readBack);
        }
        finally
        {
            RegistryService.DeleteValue(new RegistryItem(testKeyPath, "TestString"));
            RegistryService.DeleteSubKey(new RegistryItem(testKeyPath));
        }
    }

    [Fact]
    public void DeleteValue_When_Missing_Is_Treated_As_Success()
    {
        var testKeyPath = @"HKEY_CURRENT_USER\Software\optimizerDuckTests_DeleteMissing";
        try
        {
            // Ensure key exists without the value
            RegistryService.Write(new RegistryItem(testKeyPath, "_bootstrap", 1, RegistryValueKind.DWord));
            RegistryService.DeleteValue(new RegistryItem(testKeyPath, "_bootstrap"));

            var result = RegistryService.DeleteValue(new RegistryItem(testKeyPath, "DoesNotExist"));
            Assert.True(result);
        }
        finally
        {
            RegistryService.DeleteSubKey(new RegistryItem(testKeyPath));
        }
    }

    [Fact]
    public void Write_Creates_SubKey_If_Missing()
    {
        var testKeyPath = @"HKEY_CURRENT_USER\Software\optimizerDuckTests_CreateKeyOnWrite\Sub";
        try
        {
            var result = RegistryService.Write(new RegistryItem(testKeyPath, "Flag", 1, RegistryValueKind.DWord));
            Assert.True(result);

            var readBack = RegistryService.Read<int>(new RegistryItem(testKeyPath, "Flag"));
            Assert.Equal(1, readBack);
        }
        finally
        {
            RegistryService.DeleteSubKey(
                new RegistryItem(@"HKEY_CURRENT_USER\Software\optimizerDuckTests_CreateKeyOnWrite"));
        }
    }

    [Fact]
    public void Read_Converts_Types_When_Possible()
    {
        var testKeyPath = @"HKEY_CURRENT_USER\Software\optimizerDuckTests_TypeConversion";
        try
        {
            var writeOk = RegistryService.Write(new RegistryItem(testKeyPath, "Numeric", 123, RegistryValueKind.DWord));
            Assert.True(writeOk);

            var asString = RegistryService.Read<string>(new RegistryItem(testKeyPath, "Numeric"));
            Assert.Equal("123", asString);

            var asLong = RegistryService.Read<long>(new RegistryItem(testKeyPath, "Numeric"));
            Assert.Equal(123L, asLong);
        }
        finally
        {
            RegistryService.DeleteSubKey(new RegistryItem(testKeyPath));
        }
    }

    [Fact]
    public void DeleteSubKey_Removes_Entire_Tree()
    {
        var basePath = @"HKEY_CURRENT_USER\Software\optimizerDuckTests_DeleteTree";
        var childPath = basePath + "\\Child";
        try
        {
            // Create nested structure
            Assert.True(RegistryService.Write(new RegistryItem(childPath, "Leaf", "x", RegistryValueKind.String)));

            // Delete base should remove child
            Assert.True(RegistryService.DeleteSubKey(new RegistryItem(basePath)));

            // Verify
            var existsAfter = RegistryService.Read<string>(new RegistryItem(childPath, "Leaf"));
            Assert.Null(existsAfter);
        }
        finally
        {
            // Ensure cleanup if anything left
            RegistryService.DeleteSubKey(new RegistryItem(basePath));
        }
    }
}