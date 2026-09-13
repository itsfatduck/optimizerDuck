using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Exceptions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.Optimization.Providers;

namespace optimizerDuck.Domain.Revert.Steps;

/// <summary>
///     Represents a revert step that restores a Windows service to its original startup type.
/// </summary>
public class ServiceRevertStep : IRevertStep
{
    /// <summary>
    ///     Gets or sets the name of the Windows service to restore.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the original startup type to restore the service to.
    /// </summary>
    public ServiceStartupType OriginalStartupType { get; set; }

    /// <inheritdoc />
    public string Type => "Service";

    /// <inheritdoc />
    public string Description =>
        Loc.Instance["Revert.Service.Description.Restore", ServiceName, OriginalStartupType];

    /// <inheritdoc />
    public async Task<bool> ExecuteAsync(RevertContext _, ILogger logger)
    {
        var opCall = new OpCall { Logger = logger };
        var result = await ServiceProcessService
            .ChangeServiceStartupTypeAsync(
                opCall,
                new ServiceItem { Name = ServiceName, StartupType = OriginalStartupType }
            )
            .ConfigureAwait(false);

        if (!result.Ok)
        {
            // Fail closed: access-denied returns false so RevertManager records a failed
            // step; every other failure throws with provider error detail.
            var accessDenied = ServiceStrings.Format(
                ServiceStrings.ServiceInfoSkippedAccessDenied,
                ServiceName
            );
            if (string.Equals(result.Error, accessDenied, StringComparison.Ordinal))
                return false;
            throw new StepExecutionException(result.Error ?? Description, result.ErrorDetail);
        }

        var (actual, notFound) = await ServiceProcessService
            .GetStartupTypeAsync(ServiceName, opCall.Logger)
            .ConfigureAwait(false);

        // a missing service has nothing to restore.
        if (notFound)
            return true;

        // null without NotFound means the query failed; never report an unverified restore.
        if (actual is null)
            throw new StepExecutionException(
                $"Service verify failed for {ServiceName}: could not query the current startup type.",
                null
            );

        if (actual.Value != OriginalStartupType)
            throw new StepExecutionException(
                $"Service verify failed for {ServiceName}: expected {OriginalStartupType}, actual={actual.Value}",
                null
            );
        return true;
    }

    /// <inheritdoc />
    public JObject ToData()
    {
        return new JObject
        {
            [nameof(ServiceName)] = ServiceName,
            [nameof(OriginalStartupType)] = OriginalStartupType.ToString(),
        };
    }

    /// <summary>
    ///     Deserializes a <see cref="ServiceRevertStep" /> from JSON data.
    /// </summary>
    /// <param name="data">The JSON data to deserialize.</param>
    /// <returns>A new <see cref="ServiceRevertStep" /> instance.</returns>
    public static ServiceRevertStep FromData(JObject data)
    {
        return new ServiceRevertStep
        {
            ServiceName = data[nameof(ServiceName)]?.ToString() ?? string.Empty,
            OriginalStartupType = Enum.Parse<ServiceStartupType>(
                data[nameof(OriginalStartupType)]?.ToString() ?? "Manual"
            ),
        };
    }
}
