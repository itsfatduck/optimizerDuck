using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Domain.Optimizations.Models.Services;

/// <summary>Defines success criteria and error reporting for shell command execution.</summary>
public sealed class ShellPolicy
{
    /// <summary>Default policy: exit code 0 means success; error text is taken from stderr or the exit code.</summary>
    public static readonly ShellPolicy Default = new();

    /// <summary>Gets or sets a function that determines whether a shell result indicates success.</summary>
    public Func<ShellResult, bool> IsSuccess { get; init; } = r => r.ExitCode == 0;

    /// <summary>Gets or sets a function that produces an error message from a failed shell result.</summary>
    public Func<ShellResult, string?> ErrorFactory { get; init; } =
        r =>
            r.ExitCode == -1 ? Loc.Instance["Service.Shell.Error.TimedOut"]
            : string.IsNullOrWhiteSpace(r.Stderr)
                ? Loc.Instance["Service.Shell.Error.ExitCode", r.ExitCode]
            : r.Stderr;

    /// <summary>Creates a policy with a custom success function and optional error factory.</summary>
    /// <param name="isSuccess">A function that determines whether a result is successful.</param>
    /// <param name="errorFactory">An optional function that produces an error message from a failed result. Defaults to the default error factory.</param>
    /// <returns>A new <see cref="ShellPolicy"/> instance.</returns>
    public static ShellPolicy From(
        Func<ShellResult, bool> isSuccess,
        Func<ShellResult, string?>? errorFactory = null
    )
    {
        return new ShellPolicy
        {
            IsSuccess = isSuccess,
            ErrorFactory = errorFactory ?? Default.ErrorFactory,
        };
    }

    /// <summary>Creates a policy that treats specific exit codes as success.</summary>
    /// <param name="okExitCodes">The exit codes considered successful.</param>
    /// <returns>A new <see cref="ShellPolicy"/> instance.</returns>
    public static ShellPolicy SuccessExitCodes(params int[] okExitCodes)
    {
        return From(r => okExitCodes.Contains(r.ExitCode));
    }

    /// <summary>Creates a policy that treats exit codes from 0 to <paramref name="maxOk"/> as success.</summary>
    /// <param name="maxOk">The maximum exit code considered successful (inclusive).</param>
    /// <returns>A new <see cref="ShellPolicy"/> instance.</returns>
    public static ShellPolicy SuccessExitCodeRange(int maxOk)
    {
        return From(r => r.ExitCode >= 0 && r.ExitCode <= maxOk);
    }
}
