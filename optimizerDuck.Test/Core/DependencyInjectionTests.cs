namespace optimizerDuck.Test.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Services.Revert;
using optimizerDuck.Services.System.Primitives;

/// <summary>
///     Guards the composition root: the application graph must build and validate the same
///     way the app host validates it at startup, so a broken registration fails here instead
///     of on a user's machine.
/// </summary>
public class DependencyInjectionTests
{
    /// <summary>
    ///     The application graph is registered separately from the host so it can be built
    ///     here. Pages are created lazily on navigation and need a live WPF dispatcher, so
    ///     only the non-UI registrations are asserted.
    /// </summary>
    [Fact]
    public void ApplicationGraph_BuildsWithoutAPageFactoryCycle()
    {
        var configuration = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddOptimizerApplication(configuration);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }
        );

        Assert.NotNull(provider.GetRequiredService<ShellService>());
        Assert.NotNull(provider.GetRequiredService<ProcessRunner>());
        Assert.NotNull(provider.GetRequiredService<RevertManager>());
    }
}
