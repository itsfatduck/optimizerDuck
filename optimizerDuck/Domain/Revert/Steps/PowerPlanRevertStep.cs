using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Exceptions;
using optimizerDuck.Domain.Serialization;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Domain.Revert.Steps;

/// <summary>
/// Restores the previous active power scheme and removes the installed
/// optimizerDuck scheme. Verifies the restore by re-reading the active
/// scheme; deleting the installed scheme is best-effort after a verified
/// restore (a missing scheme at delete time counts as already reverted).
/// </summary>
public class PowerPlanRevertStep : IRevertStep
{
    public Guid PreviousSchemeId { get; set; }

    public Guid InstalledSchemeId { get; set; }

    /// <inheritdoc />
    public string Type => "PowerPlan";

    /// <inheritdoc />
    public string Description => Loc.Instance["Revert.PowerPlan.Description", PreviousSchemeId];

    /// <inheritdoc />
    public Task<bool> ExecuteAsync(RevertContext context, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        var powerPlans = context.PowerPlans;

        var restored = false;
        if (PreviousSchemeId != Guid.Empty)
        {
            var activation = powerPlans.SetActiveScheme(PreviousSchemeId, logger);
            var setActive = activation.Result;
            if (!setActive.Ok)
                throw new StepExecutionException(
                    Loc.Instance["Revert.PowerPlan.Error.RestoreFailed", setActive.Error ?? ""],
                    setActive.ErrorDetail
                );
            var active = powerPlans.GetActiveSchemeId();
            if (active is null)
                throw new StepExecutionException(
                    Loc.Instance["Revert.PowerPlan.Error.VerifyFailed", PreviousSchemeId],
                    null
                );
            if (active.Value != PreviousSchemeId)
                throw new StepExecutionException(
                    Loc.Instance[
                        "Revert.PowerPlan.Error.VerifyMismatch",
                        PreviousSchemeId,
                        active.Value
                    ],
                    null
                );
            restored = true;
        }

        if (InstalledSchemeId != Guid.Empty)
        {
            // Never delete the scheme we just restored: installing over the
            // same GUID means previous == installed.
            if (InstalledSchemeId != PreviousSchemeId)
            {
                if (powerPlans.SchemeExists(InstalledSchemeId))
                {
                    var deleted = powerPlans.DeleteScheme(InstalledSchemeId, logger);
                    if (!deleted.Ok)
                        throw new StepExecutionException(
                            Loc.Instance[
                                "Revert.PowerPlan.Error.DeleteFailed",
                                deleted.Error ?? ""
                            ],
                            deleted.ErrorDetail
                        );
                }
            }
        }

        if (!restored && InstalledSchemeId == Guid.Empty)
            throw new StepExecutionException(
                Loc.Instance["Revert.PowerPlan.Error.NothingToDo"],
                ""
            );

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public JsonObject ToData()
    {
        return new JsonObject
        {
            [nameof(PreviousSchemeId)] = PreviousSchemeId.ToString(),
            [nameof(InstalledSchemeId)] = InstalledSchemeId.ToString(),
        };
    }

    public static PowerPlanRevertStep FromData(JsonNode? data)
    {
        if (data is not JsonObject obj)
            throw new StepExecutionException("Invalid PowerPlan revert data.", "");
        if (
            !Guid.TryParse(
                JsonNodeHelpers.AsString(obj[nameof(PreviousSchemeId)]),
                out var previous
            )
            || !Guid.TryParse(
                JsonNodeHelpers.AsString(obj[nameof(InstalledSchemeId)]),
                out var installed
            )
        )
            throw new StepExecutionException("Invalid PowerPlan revert data.", "");
        return new PowerPlanRevertStep
        {
            PreviousSchemeId = previous,
            InstalledSchemeId = installed,
        };
    }
}
