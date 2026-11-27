using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Exceptions;
using optimizerDuck.Domain.Optimizations.Models.Power;
using optimizerDuck.Domain.Serialization;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Domain.Revert.Steps;

/// <summary>
/// Restores exact previous AC/DC values captured before a power-setting
/// write. Fails closed: an unreadable value after restore is a failure,
/// never a silent success.
/// </summary>
public class PowerSettingRevertStep : IRevertStep
{
    public Guid SchemeId { get; set; }

    public Guid SubgroupId { get; set; }

    public Guid SettingId { get; set; }

    public uint PreviousAcValue { get; set; }

    public uint PreviousDcValue { get; set; }

    /// <inheritdoc />
    public string Type => "PowerSetting";

    /// <inheritdoc />
    public string Description =>
        Loc.Instance["Revert.PowerSetting.Description", SchemeId, SubgroupId, SettingId];

    /// <inheritdoc />
    public Task<bool> ExecuteAsync(RevertContext context, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        var write = context.PowerPlans.SetSetting(
            SchemeId,
            SubgroupId,
            SettingId,
            PreviousAcValue,
            PreviousDcValue,
            logger
        );
        var result = write.Result;
        if (!result.Ok)
            throw new StepExecutionException(result.Error ?? Description, result.ErrorDetail);

        var ac = context.PowerPlans.GetSettingValue(
            SchemeId,
            SubgroupId,
            SettingId,
            PowerSource.Ac
        );
        var dc = context.PowerPlans.GetSettingValue(
            SchemeId,
            SubgroupId,
            SettingId,
            PowerSource.Dc
        );
        if (ac is null || dc is null)
            throw new StepExecutionException(
                $"Power setting verify failed for {SchemeId}/{SubgroupId}/{SettingId}: could not read the values.",
                null
            );
        if (ac.Value != PreviousAcValue || dc.Value != PreviousDcValue)
            throw new StepExecutionException(
                $"Power setting verify failed for {SchemeId}/{SubgroupId}/{SettingId}: expected AC={PreviousAcValue} DC={PreviousDcValue}, actual AC={ac} DC={dc}",
                null
            );
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public JsonObject ToData()
    {
        return new JsonObject
        {
            [nameof(SchemeId)] = SchemeId.ToString(),
            [nameof(SubgroupId)] = SubgroupId.ToString(),
            [nameof(SettingId)] = SettingId.ToString(),
            [nameof(PreviousAcValue)] = PreviousAcValue,
            [nameof(PreviousDcValue)] = PreviousDcValue,
        };
    }

    public static PowerSettingRevertStep FromData(JsonNode? data)
    {
        if (data is not JsonObject obj)
            throw new StepExecutionException("Invalid PowerSetting revert data.", "");
        if (
            !Guid.TryParse(JsonNodeHelpers.AsString(obj[nameof(SchemeId)]), out var scheme)
            || !Guid.TryParse(JsonNodeHelpers.AsString(obj[nameof(SubgroupId)]), out var subgroup)
            || !Guid.TryParse(JsonNodeHelpers.AsString(obj[nameof(SettingId)]), out var setting)
            || !JsonNodeHelpers.TryGetUInt32(obj[nameof(PreviousAcValue)], out var ac)
            || !JsonNodeHelpers.TryGetUInt32(obj[nameof(PreviousDcValue)], out var dc)
        )
            throw new StepExecutionException("Invalid PowerSetting revert data.", "");
        return new PowerSettingRevertStep
        {
            SchemeId = scheme,
            SubgroupId = subgroup,
            SettingId = setting,
            PreviousAcValue = ac,
            PreviousDcValue = dc,
        };
    }
}
