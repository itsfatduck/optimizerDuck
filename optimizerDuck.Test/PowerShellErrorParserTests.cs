using optimizerDuck.Core.Extensions;

namespace optimizerDuck.Test;

public class PowerShellErrorParserTests
{
    [Fact]
    public void ParseCliXml_ShouldReturnEmpty_WhenInputIsWhitespace()
    {
        var result = "   ".ParseCliXml();
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ParseCliXml_ShouldParseSimpleClixml()
    {
        var xml = @"#< CLIXML
<Objs Version='1.1.0.1'>
    <S>Access is denied</S>
</Objs>";

        var result = xml.ParseCliXml();
        Assert.Contains("Access is denied", result);
    }

    [Fact]
    public void ParseCliXml_ShouldHandleEncodedNewlines()
    {
        var xml = @"#< CLIXML
<Objs Version='1.1.0.1'>
    <S>Error_x000D__x000A_Second Line</S>
</Objs>";

        var result = xml.ParseCliXml();
        Assert.Contains("Error", result);
        Assert.Contains("Second Line", result);
    }

    [Fact]
    public void ParseCliXml_ShouldHandleListOfObjects()
    {
        var xml = @"#< CLIXML
<Objs Version='1.1.0.1'>
  <Obj RefId='0'>
    <TN RefId='0'>
      <T>System.String</T>
    </TN>
    <ToString>First</ToString>
  </Obj>
  <Obj RefId='1'>
    <TN RefId='1'>
      <T>System.String</T>
    </TN>
    <ToString>Second</ToString>
  </Obj>
</Objs>";

        var result = xml.ParseCliXml();
        Assert.Contains("First", result);
        Assert.Contains("Second", result);
    }

    [Fact]
    public void ParseCliXml_ShouldFallback_WhenInvalidXml()
    {
        var invalidXml = "<S>Error message missing end tag";
        var result = invalidXml.ParseCliXml();
        Assert.Contains("Error message", result);
    }

    [Fact]
    public void ParseCliXml_ShouldWrapNonXmlText()
    {
        var text = "Access is denied";
        var result = text.ParseCliXml();
        Assert.Contains("Access is denied", result);
    }

    [Fact]
    public void ParseCliXml_ShouldIgnoreBOM()
    {
        var bom = "\uFEFF#< CLIXML<Objs><S>BOM handled</S></Objs>";
        var result = bom.ParseCliXml();
        Assert.Contains("BOM handled", result);
    }

    [Fact]
    public void ParseCliXml_ShouldReturnEmpty_OnEmptyObjs()
    {
        var xml = "#< CLIXML<Objs></Objs>";
        var result = xml.ParseCliXml();
        Assert.Equal(string.Empty, result);
    }
}