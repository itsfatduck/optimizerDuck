using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Exceptions;
using optimizerDuck.Domain.Execution;
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
        var call = new OpCall { Logger = logger };

        var restored = false;
        if (PreviousSchemeId != Guid.Empty)
        {
            var setActive = powerPlans.SetActiveScheme(call, PreviousSchemeId);
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
                    var deleted = powerPlans.DeleteScheme(call, InstalledSchemeId);
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
    public JObject ToData()
    {
        return new JObject
        {
            [nameof(PreviousSchemeId)] = PreviousSchemeId.ToString(),
            [nameof(InstalledSchemeId)] = InstalledSchemeId.ToString(),
        };
    }

    public static PowerPlanRevertStep FromData(JToken data)
    {
        if (data is not JObject obj)
            throw new StepExecutionException("Invalid PowerPlan revert data.", "");
        if (
            !Guid.TryParse(obj[nameof(PreviousSchemeId)]?.ToString(), out var previous)
            || !Guid.TryParse(obj[nameof(InstalledSchemeId)]?.ToString(), out var installed)
        )
            throw new StepExecutionException("Invalid PowerPlan revert data.", "");
        return new PowerPlanRevertStep
        {
            PreviousSchemeId = previous,
            InstalledSchemeId = installed,
        };
    }
}
