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
}