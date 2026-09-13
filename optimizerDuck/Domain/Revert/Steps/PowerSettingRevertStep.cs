using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Exceptions;
using optimizerDuck.Domain.Optimizations.Models.Power;
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
    public JObject ToData()
    {
        return new JObject
        {
            [nameof(SchemeId)] = SchemeId.ToString(),
            [nameof(SubgroupId)] = SubgroupId.ToString(),
            [nameof(SettingId)] = SettingId.ToString(),
            [nameof(PreviousAcValue)] = PreviousAcValue,
            [nameof(PreviousDcValue)] = PreviousDcValue,
        };
    }

    public static PowerSettingRevertStep FromData(JToken data)
    {
        if (data is not JObject obj)
            throw new StepExecutionException("Invalid PowerSetting revert data.", "");
        if (
            !Guid.TryParse(obj[nameof(SchemeId)]?.ToString(), out var scheme)
            || !Guid.TryParse(obj[nameof(SubgroupId)]?.ToString(), out var subgroup)
            || !Guid.TryParse(obj[nameof(SettingId)]?.ToString(), out var setting)
            || !uint.TryParse(obj[nameof(PreviousAcValue)]?.ToString(), out var ac)
            || !uint.TryParse(obj[nameof(PreviousDcValue)]?.ToString(), out var dc)
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
