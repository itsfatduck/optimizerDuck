using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Services.System.Primitives;

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

    [Fact]
    public async Task AFailedCommandRecordsTheExitCodeItFailedWith()
    {
        var shell = NewShell();
        var call = new OpCall { Logger = NullLogger.Instance };

        var result = await shell.CMDAsync(
            "exit 3",
            call,
            ct: TestContext.Current.CancellationToken
        );

        Assert.False(result.Ok);
        var step = Assert.Single(call.Changes.Changes);
        Assert.Equal(3, step.NativeErrorCode);
    }

    [Fact]
    public async Task ACommandThatSucceedsRecordsNoCode()
    {
        var shell = NewShell();
        var call = new OpCall { Logger = NullLogger.Instance };

        var result = await shell.CMDAsync(
            "exit 0",
            call,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Ok);
        Assert.Null(Assert.Single(call.Changes.Changes).NativeErrorCode);
    }
}
