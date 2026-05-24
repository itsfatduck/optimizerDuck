using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Services.OptimizationServices;

namespace optimizerDuck.Test.Services.OptimizationServices;

public class ShellPolicyTests
{
    [Fact]
    public void DefaultPolicy_ExitCodeZero_ReturnsSuccess()
    {
        var result = new ShellResult
        {
            Command = "test",
            Stdout = "",
            Stderr = "",
            ExitCode = 0,
            Duration = TimeSpan.Zero,
        };

        Assert.True(ShellPolicy.Default.IsSuccess(result));
    }

    [Fact]
    public void DefaultPolicy_NonZeroExitCode_ReturnsFailure()
    {
        var result = new ShellResult
        {
            Command = "test",
            Stdout = "",
            Stderr = "",
            ExitCode = 1,
            Duration = TimeSpan.Zero,
        };

        Assert.False(ShellPolicy.Default.IsSuccess(result));
    }

    [Fact]
    public void DefaultPolicy_WithStderr_ReturnsStderrAsError()
    {
        var result = new ShellResult
        {
            Command = "test",
            Stdout = "",
            Stderr = "Access denied",
            ExitCode = 5,
            Duration = TimeSpan.Zero,
        };

        var error = ShellPolicy.Default.ErrorFactory(result);
        Assert.Equal("Access denied", error);
    }

    [Fact]
    public void DefaultPolicy_WithoutStderr_ReturnsExitCodeMessage()
    {
        var result = new ShellResult
        {
            Command = "test",
            Stdout = "",
            Stderr = "",
            ExitCode = 3,
            Duration = TimeSpan.Zero,
        };

        var error = ShellPolicy.Default.ErrorFactory(result);
        Assert.Contains("3", error, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultPolicy_TimedOut_ReturnsTimeoutMessage()
    {
        var result = new ShellResult
        {
            Command = "test",
            Stdout = "",
            Stderr = "",
            ExitCode = -1,
            Duration = TimeSpan.FromSeconds(30),
        };

        var error = ShellPolicy.Default.ErrorFactory(result);
        Assert.Contains("timed out", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SuccessExitCodes_WithAllowedExitCode_ReturnsSuccess()
    {
        var policy = ShellPolicy.SuccessExitCodes(0, 1, 2);

        var result = new ShellResult
        {
            Command = "test",
            Stdout = "",
            Stderr = "",
            ExitCode = 1,
            Duration = TimeSpan.Zero,
        };

        Assert.True(policy.IsSuccess(result));
    }

    [Fact]
    public void SuccessExitCodes_WithDisallowedExitCode_ReturnsFailure()
    {
        var policy = ShellPolicy.SuccessExitCodes(0, 1);

        var result = new ShellResult
        {
            Command = "test",
            Stdout = "",
            Stderr = "",
            ExitCode = 3,
            Duration = TimeSpan.Zero,
        };

        Assert.False(policy.IsSuccess(result));
    }

    [Fact]
    public void SuccessExitCodeRange_WithInRange_ReturnsSuccess()
    {
        var policy = ShellPolicy.SuccessExitCodeRange(5);

        var result = new ShellResult
        {
            Command = "test",
            Stdout = "",
            Stderr = "",
            ExitCode = 3,
            Duration = TimeSpan.Zero,
        };

        Assert.True(policy.IsSuccess(result));
    }

    [Fact]
    public void SuccessExitCodeRange_WithNegativeExitCode_ReturnsFailure()
    {
        var policy = ShellPolicy.SuccessExitCodeRange(5);

        var result = new ShellResult
        {
            Command = "test",
            Stdout = "",
            Stderr = "",
            ExitCode = -1,
            Duration = TimeSpan.Zero,
        };

        Assert.False(policy.IsSuccess(result));
    }

    [Fact]
    public void SuccessExitCodeRange_WithOutOfRange_ReturnsFailure()
    {
        var policy = ShellPolicy.SuccessExitCodeRange(5);

        var result = new ShellResult
        {
            Command = "test",
            Stdout = "",
            Stderr = "",
            ExitCode = 6,
            Duration = TimeSpan.Zero,
        };

        Assert.False(policy.IsSuccess(result));
    }

    [Fact]
    public void From_CustomPolicy_UsesCustomPredicate()
    {
        var policy = ShellPolicy.From(r => r.ExitCode == 42);

        var successResult = new ShellResult
        {
            Command = "test",
            Stdout = "",
            Stderr = "",
            ExitCode = 42,
            Duration = TimeSpan.Zero,
        };

        var failResult = new ShellResult
        {
            Command = "test",
            Stdout = "",
            Stderr = "",
            ExitCode = 0,
            Duration = TimeSpan.Zero,
        };

        Assert.True(policy.IsSuccess(successResult));
        Assert.False(policy.IsSuccess(failResult));
    }

    [Fact]
    public void From_WithCustomErrorFactory_UsesCustomFactory()
    {
        var policy = ShellPolicy.From(
            r => r.ExitCode == 0,
            r => $"Custom error: exit code {r.ExitCode}"
        );

        var result = new ShellResult
        {
            Command = "test",
            Stdout = "",
            Stderr = "",
            ExitCode = 1,
            Duration = TimeSpan.Zero,
        };

        Assert.Equal("Custom error: exit code 1", policy.ErrorFactory(result));
    }

    [Fact]
    public void From_WithoutErrorFactory_UsesDefault()
    {
        var policy = ShellPolicy.From(r => r.ExitCode is 0 or 1);

        var result = new ShellResult
        {
            Command = "test",
            Stdout = "",
            Stderr = "Permission denied",
            ExitCode = 2,
            Duration = TimeSpan.Zero,
        };

        Assert.Equal("Permission denied", policy.ErrorFactory(result));
    }

    [Fact]
    public void DefaultPolicy_IsSingleton()
    {
        Assert.Same(ShellPolicy.Default, ShellPolicy.Default);
    }

    [Fact]
    public void DefaultPolicy_ExitCodeNegativeOne_AlwaysReturnsTimeoutMessage()
    {
        var result = new ShellResult
        {
            Command = "test",
            Stdout = "",
            Stderr = "File not found",
            ExitCode = -1,
            Duration = TimeSpan.FromSeconds(30),
        };

        var error = ShellPolicy.Default.ErrorFactory(result);
        Assert.Contains("timed out", error, StringComparison.OrdinalIgnoreCase);
    }
}
