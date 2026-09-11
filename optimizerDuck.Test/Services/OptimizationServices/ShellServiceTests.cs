using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Services.Optimization.Providers;

namespace optimizerDuck.Test.Services.OptimizationServices;

public class ShellServiceTests
{
    private static ShellService NewShell() =>
        new(new ProcessRunner(120000, NullLogger<ProcessRunner>.Instance));

    [Fact]
    public async Task Cmd_ExitZero_ReturnsSuccessExitCode()
    {
        var shell = NewShell();
        var result = await shell.QueryCMDAsync(
            "exit 0",
            NullLogger.Instance,
            ct: TestContext.Current.CancellationToken
        );

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task Cmd_ExitOne_ReturnsNonZeroExitCode()
    {
        var shell = NewShell();
        var result = await shell.QueryCMDAsync(
            "exit 1",
            NullLogger.Instance,
            ct: TestContext.Current.CancellationToken
        );

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task PowerShell_SimpleCommand_ReturnsSuccessExitCode()
    {
        var shell = NewShell();
        var result = await shell.QueryPowerShellAsync(
            "Write-Output 'ok'",
            NullLogger.Instance,
            ct: TestContext.Current.CancellationToken
        );

        Assert.Equal(0, result.ExitCode);
    }
}
