using optimizerDuck.Common.Extensions;

namespace optimizerDuck.Test.Common.Extensions;

public class TextExtensionsTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void ParseCliXml_EmptyOrWhitespace_ReturnsEmpty(string? input, string expected)
    {
        Assert.Equal(expected, input.ParseCliXml());
    }

    [Fact]
    public void ParseCliXml_PlainTextError_ReturnsTrimmedDecodedText()
    {
        const string input = "  Access denied_x000D__x000A_  ";

        Assert.Equal("Access denied", input.ParseCliXml());
    }

    [Fact]
    public void ParseCliXml_ErrorStreamElements_DecodesLinesAndSkipsProgress()
    {
        const string cliXml = """
            #< CLIXML
            <Objs Version="1.1.0.1" xmlns="http://schemas.microsoft.com/powershell/2004/04">
              <S S="progress">Preparing modules</S>
              <S S="Error">Remove-AppxPackage : Access denied._x000D__x000A_</S>
              <S S="Error">At line:1 char:1_x000D__x000A_</S>
            </Objs>
            """;

        var parsed = cliXml.ParseCliXml();

        Assert.Equal(
            $"Remove-AppxPackage : Access denied.{Environment.NewLine}At line:1 char:1",
            parsed
        );
    }

    [Fact]
    public void ParseCliXml_ObjWithToString_PrefersToStringWithoutDuplicatingMsChild()
    {
        const string cliXml = """
            #< CLIXML
            <Objs Version="1.1.0.1" xmlns="http://schemas.microsoft.com/powershell/2004/04">
              <Obj RefId="0">
                <TN RefId="0"><T>System.Management.Automation.ErrorRecord</T></TN>
                <ToString>Package &lt;foo&gt; failed</ToString>
                <MS><S N="Message">Package &lt;foo&gt; failed</S></MS>
              </Obj>
            </Objs>
            """;

        Assert.Equal("Package <foo> failed", cliXml.ParseCliXml());
    }

    [Fact]
    public void ParseCliXml_EscapedAngleBracketsAndHex_DecodesAfterParsing()
    {
        const string cliXml = """
            #< CLIXML
            <Objs Version="1.1.0.1" xmlns="http://schemas.microsoft.com/powershell/2004/04">
              <S S="Error">a _x003C_ b_x0009_c</S>
            </Objs>
            """;

        Assert.Equal("a < b\tc", cliXml.ParseCliXml());
    }

    [Fact]
    public void ParseCliXml_MalformedXml_FallsBackToCleanedText()
    {
        const string malformed = "#< CLIXML\r\n<Objs><S S=\"Error\">broken_x000D__x000A_";

        Assert.Equal("<Objs><S S=\"Error\">broken", malformed.ParseCliXml());
    }

    [Fact]
    public void ParseCliXml_PlainTextWithXmlCharacters_DoesNotLeakWrapperTags()
    {
        const string input = "error: value < 10 & path <file> failed";

        Assert.Equal(input, input.ParseCliXml());
    }

    [Fact]
    public void ParseCliXml_CliXmlFragmentWithoutObjsWrapper_ExtractsText()
    {
        const string input = "<S S=\"Error\">Missing key_x000D__x000A_</S>";

        Assert.Equal("Missing key", input.ParseCliXml());
    }
}
