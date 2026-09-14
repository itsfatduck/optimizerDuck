using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Exceptions;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Domain.Revert.Steps;

/// <summary>
///     Restores the hibernation file state captured before it was removed, through the documented
///     power information callback instead of a <c>powercfg /h</c> shell command.
/// </summary>
public class HibernationRevertStep : IRevertStep
{
    /// <summary>
    ///     Gets or sets a value that indicates whether the hibernation file existed before the
    ///     change, so reverting commits it back.
    /// </summary>
    public bool WasPresent { get; set; }

    /// <inheritdoc />
    public string Type => "Hibernation";

    /// <inheritdoc />
    public string Description => Loc.Instance["Revert.Hibernation.Description"];

    /// <inheritdoc />
    public Task<bool> ExecuteAsync(RevertContext context, ILogger logger)
    {
        var result = HibernationService.SetHibernationFile(WasPresent);

        if (!result.Succeeded)
        {
            var error =
                result.ExceptionText
                ?? Loc.Instance[
                    "Revert.Hibernation.Error.RestoreFailed",
                    $"0x{result.NativeStatus:X8}"
                ];
            logger.LogWarning(
                "[HIBERNATION][REVERT][FAIL] NTSTATUS 0x{Status:X8}",
                result.NativeStatus
            );
            throw new StepExecutionException(error, result.ExceptionText ?? string.Empty);
        }

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public JObject ToData()
    {
        return new JObject { [nameof(WasPresent)] = WasPresent };
    }

    /// <summary>
    ///     Deserializes a <see cref="HibernationRevertStep" /> from JSON data. Defaults to
    ///     "was present" when the flag is missing, matching the optimization's fail-safe default.
    /// </summary>
    public static HibernationRevertStep FromData(JObject data)
    {
        return new HibernationRevertStep
        {
            WasPresent = data[nameof(WasPresent)]?.Value<bool>() ?? true,
        };
    }
}
