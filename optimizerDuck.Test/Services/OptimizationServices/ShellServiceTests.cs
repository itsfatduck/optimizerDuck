using optimizerDuck.Services.OptimizationServices;

namespace optimizerDuck.Test.Services.OptimizationServices;

public class ShellServiceTests
{
    [Fact]
    public void Cmd_ExitZero_ReturnsSuccessExitCode()
    {
        var result = ShellService.CMD("exit 0");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Cmd_ExitOne_ReturnsNonZeroExitCode()
    {
        var result = ShellService.CMD("exit 1");

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void PowerShell_SimpleCommand_ReturnsSuccessExitCode()
    {
        var result = ShellService.PowerShell("Write-Output 'ok'");

        Assert.Equal(0, result.ExitCode);
    }

    [Theory]
    [InlineData("Tiếng Việt có dấu")]
    [InlineData("Zażółć gęślą jaźń")]
    [InlineData("Español – información")]
    [InlineData("Français – élève")]
    [InlineData("Deutsch – äußern")]
    public void PowerShell_MultiLanguageUnicode_ReturnsCorrectString(string text)
    {
        var result = ShellService.PowerShell($"Write-Output '{text}'");

        Assert.Contains(text, result.Stdout);
    }
}