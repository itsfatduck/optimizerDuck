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
    ///     Gets or sets whether the hibernation file existed before the change. Null means the
    ///     state could not be read, so there is nothing to restore and a revert leaves the
    ///     system alone instead of guessing and creating a multi gigabyte file.
    /// </summary>
    public bool? WasPresent { get; set; }

    /// <summary>Whether the state could not be read when the change was applied.</summary>
    public bool StateUnknown => WasPresent is null;

    /// <inheritdoc />
    public string Type => "Hibernation";

    /// <inheritdoc />
    public string Description =>
        StateUnknown
            ? Loc.Instance["Revert.Hibernation.Description.Unknown"]
            : Loc.Instance["Revert.Hibernation.Description"];

    /// <inheritdoc />
    public Task<bool> ExecuteAsync(RevertContext context, ILogger logger)
    {
        if (WasPresent is not { } present)
        {
            logger.LogWarning(
                "[HIBERNATION][REVERT][SKIP] the previous state was unknown, nothing restored"
            );
            return Task.FromResult(true);
        }

        var result = HibernationService.SetHibernationFile(present);

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
        return new JObject
        {
            [nameof(WasPresent)] = WasPresent is { } present
                ? new JValue(present)
                : JValue.CreateNull(),
        };
    }

    /// <summary>
    ///     Deserializes a <see cref="HibernationRevertStep" /> from JSON data. Files written
    ///     before this change always carry a boolean; a missing or null value means the state was
    ///     not known, which restores nothing.
    /// </summary>
    public static HibernationRevertStep FromData(JObject data)
    {
        var token = data[nameof(WasPresent)];
        return new HibernationRevertStep
        {
            WasPresent =
                token is null || token.Type == JTokenType.Null ? null : token.Value<bool>(),
        };
    }
}
