using Microsoft.Win32;
using optimizerDuck.Domain.Optimizations.Models.Services;

namespace optimizerDuck.Test.Domain.Optimizations.Models.Services;

public class RegistryItemKindDetectionTests
{
    [Fact]
    public void Constructor_WithIntValue_DetectsDWord()
    {
        var item = new RegistryItem("HKLM\\Test", "Value", 42);
        Assert.Equal(RegistryValueKind.DWord, item.Kind);
    }

    [Fact]
    public void Constructor_WithLongValue_DetectsQWord()
    {
        var item = new RegistryItem("HKLM\\Test", "Value", 9999999999L);
        Assert.Equal(RegistryValueKind.QWord, item.Kind);
    }

    [Fact]
    public void Constructor_WithStringValue_DetectsString()
    {
        var item = new RegistryItem("HKLM\\Test", "Value", "Hello");
        Assert.Equal(RegistryValueKind.String, item.Kind);
    }

    [Fact]
    public void Constructor_WithStringArrayValue_DetectsMultiString()
    {
        var item = new RegistryItem("HKLM\\Test", "Value", new[] { "a", "b", "c" });
        Assert.Equal(RegistryValueKind.MultiString, item.Kind);
    }

    [Fact]
    public void Constructor_WithByteArrayValue_DetectsBinary()
    {
        var item = new RegistryItem("HKLM\\Test", "Value", new byte[] { 0x01, 0x02, 0x03 });
        Assert.Equal(RegistryValueKind.Binary, item.Kind);
    }

    [Fact]
    public void Constructor_WithNullValue_DetectsUnknown()
    {
        var item = new RegistryItem("HKLM\\Test", "Value", null as object);
        Assert.Equal(RegistryValueKind.Unknown, item.Kind);
    }

    [Fact]
    public void Constructor_WithExplicitKind_DoesNotAutoDetect()
    {
        var item = new RegistryItem("HKLM\\Test", "Value", 42, RegistryValueKind.QWord);
        Assert.Equal(RegistryValueKind.QWord, item.Kind);
    }

    [Fact]
    public void Constructor_WithoutValue_SetsKindUnknown()
    {
        var item = new RegistryItem("HKLM\\Test", "Value");
        Assert.Equal(RegistryValueKind.Unknown, item.Kind);
        Assert.Null(item.Value);
    }

    [Fact]
    public void Constructor_KeyOnly_SetsKindUnknownAndNullName()
    {
        var item = new RegistryItem("HKLM\\Test");
        Assert.Equal(RegistryValueKind.Unknown, item.Kind);
        Assert.Null(item.Name);
        Assert.Null(item.Value);
    }

    [Fact]
    public void Constructor_WithBoolValue_DetectsUnknown()
    {
        var item = new RegistryItem("HKLM\\Test", "Value", true);
        Assert.Equal(RegistryValueKind.Unknown, item.Kind);
    }
}
